import zipfile
from pathlib import Path

import cdsapi
import pandas as pd
import xarray as xr

# Os mesmos pontos do soilgrids.py e do sentinel1.py
PONTOS = [
    {"regiao": "Chokwe", "ponto": "CHK_01", "lat": -24.45, "lon": 33.05},
    {"regiao": "Chokwe", "ponto": "CHK_02", "lat": -24.55, "lon": 33.10},
    {"regiao": "Massinga", "ponto": "MAS_01", "lat": -23.25, "lon": 35.35},
    {"regiao": "Massinga", "ponto": "MAS_02", "lat": -23.40, "lon": 35.30},
]

ANO_INICIO = 2019
ANO_FIM = 2024
PASTA = Path(__file__).parent / "dados"
PASTA_BRUTOS = PASTA / "brutos" / "ascat"
PASTA_BRUTOS.mkdir(parents=True, exist_ok=True)


def descarregar_ano(cliente, ano):
    """Descarrega as médias de 10 dias do ASCAT (sensor 'active' do C3S) para um ano inteiro."""
    destino = PASTA_BRUTOS / f"ascat_{ano}.zip"
    if destino.exists():
        print("  já descarregado:", destino.name)
        return
    pedido = {
        "variable": ["surface_soil_moisture_saturation"],
        "type_of_sensor": ["active"],
        "time_aggregation": ["10_day_average"],
        "year": [str(ano)],
        "month": [f"{m:02d}" for m in range(1, 13)],
        "day": ["01", "11", "21"],
        "type_of_record": ["cdr"],
        "version": ["v202505"],
    }
    cliente.retrieve("satellite-soil-moisture", pedido, str(destino))


def ler_grelha():
    """Abre todos os ficheiros NetCDF e junta a zona de Chókwè e Massinga numa só série temporal."""
    for ficheiro in sorted(PASTA_BRUTOS.glob("*.zip")):
        with zipfile.ZipFile(ficheiro) as z:
            z.extractall(PASTA_BRUTOS / ficheiro.stem)

    partes = []
    for nc in sorted(PASTA_BRUTOS.glob("*/*.nc")):
        with xr.open_dataset(nc) as ds:
            # A latitude vem de norte para sul, por isso o slice vai do maior para o menor
            partes.append(ds["sm"].sel(lat=slice(-22.5, -25.5), lon=slice(32.0, 36.0)).load())
    return xr.concat(partes, dim="time").sortby("time")


def escolher_celula(sm, lat, lon):
    """Célula ASCAT mais próxima do ponto que tenha dados em pelo menos metade das datas.

    As células junto à costa vêm vazias (o ASCAT descarta-as por causa da água),
    por isso a célula mais próxima nem sempre serve.
    """
    validos = sm.notnull().mean("time")
    perto = validos.sel(lat=slice(lat + 0.3, lat - 0.3), lon=slice(lon - 0.3, lon + 0.3))
    celulas = perto.to_dataframe(name="frac").reset_index()
    celulas = celulas[celulas["frac"] >= 0.5]
    celulas["distancia"] = (celulas["lat"] - lat) ** 2 + (celulas["lon"] - lon) ** 2
    melhor = celulas.sort_values("distancia").iloc[0]
    return float(melhor["lat"]), float(melhor["lon"])


cliente = cdsapi.Client()
for ano in range(ANO_INICIO, ANO_FIM + 1):
    print("A descarregar", ano, "...")
    descarregar_ano(cliente, ano)

sm = ler_grelha()
linhas = []
for ponto in PONTOS:
    cel_lat, cel_lon = escolher_celula(sm, ponto["lat"], ponto["lon"])
    print(ponto["ponto"], "-> célula ASCAT", cel_lat, cel_lon)
    serie = sm.sel(lat=cel_lat, lon=cel_lon).to_pandas()
    for data, valor in serie.items():
        linhas.append({**ponto, "data": data, "ASCAT_SM": valor, "ascat_lat": cel_lat, "ascat_lon": cel_lon})

tabela = pd.DataFrame(linhas)
print("Valores em falta por ponto:", tabela.groupby("ponto")["ASCAT_SM"].apply(lambda s: s.isna().sum()).to_dict())
tabela = tabela.dropna(subset=["ASCAT_SM"])
tabela["ASCAT_SM"] = tabela["ASCAT_SM"].round(2)  # % de saturação (0 = seco, 100 = saturado)
tabela.to_csv(PASTA / "processados" / "ascat.csv", index=False)
print("Guardado:", len(tabela), "linhas em", PASTA / "processados" / "ascat.csv")
