import ee
import pandas as pd
from pathlib import Path

ee.Initialize(project="secagrupo1")

# Os mesmos pontos do soilgrids.py, para depois juntar as duas tabelas
PONTOS = [
    {"regiao": "Chokwe", "ponto": "CHK_01", "lat": -24.45, "lon": 33.05},
    {"regiao": "Chokwe", "ponto": "CHK_02", "lat": -24.55, "lon": 33.10},
    {"regiao": "Massinga", "ponto": "MAS_01", "lat": -23.25, "lon": 35.35},
    {"regiao": "Massinga", "ponto": "MAS_02", "lat": -23.40, "lon": 35.30},
]

ANO_INICIO = 2019
ANO_FIM = 2024
RAIO_M = 1000       # média do radar num círculo de 1 km à volta de cada ponto
ORBITA = 6          # órbita descendente que cobre Chókwè e Massinga: mesma geometria em todas as imagens


def criar_areas():
    """Transforma a lista PONTOS em círculos no Earth Engine."""
    areas = []
    for p in PONTOS:
        circulo = ee.Geometry.Point([p["lon"], p["lat"]]).buffer(RAIO_M)
        areas.append(ee.Feature(circulo, p))
    return ee.FeatureCollection(areas)


def recolher_ano(areas, ano):
    """Devolve uma lista com o valor médio de VV e VH por ponto e por imagem, num ano."""
    imagens = (
        ee.ImageCollection("COPERNICUS/S1_GRD")
        .filterBounds(areas)
        .filterDate(f"{ano}-01-01", f"{ano + 1}-01-01")
        .filter(ee.Filter.eq("instrumentMode", "IW"))
        .filter(ee.Filter.eq("relativeOrbitNumber_start", ORBITA))
        .select(["VV", "VH"])
    )

    def media_por_ponto(imagem):
        data = imagem.date().format("YYYY-MM-dd")
        medias = imagem.reduceRegions(areas, ee.Reducer.mean(), scale=10)
        return medias.map(lambda f: f.set("data", data))

    resultado = imagens.map(media_por_ponto).flatten().getInfo()
    return [f["properties"] for f in resultado["features"]]


areas = criar_areas()
linhas = []
for ano in range(ANO_INICIO, ANO_FIM + 1):
    print("A recolher", ano, "...")
    linhas += recolher_ano(areas, ano)

tabela = pd.DataFrame(linhas).dropna(subset=["VV"])
tabela["data"] = pd.to_datetime(tabela["data"])
tabela = tabela.sort_values(["ponto", "data"])

pasta = Path(__file__).parent / "dados"
tabela.to_csv(pasta / "brutos" / "sentinel1_imagens.csv", index=False)

# Índice de humidade por change detection, calculado separadamente para cada ponto:
# 0 = o mais seco observado nesse ponto, 1 = o mais húmido
p5 = tabela.groupby("ponto")["VV"].transform(lambda s: s.quantile(0.05))
p95 = tabela.groupby("ponto")["VV"].transform(lambda s: s.quantile(0.95))
tabela["S1_SoilMoisture"] = ((tabela["VV"] - p5) / (p95 - p5)).clip(0, 1).round(3)

tabela.to_csv(pasta / "processados" / "sentinel1.csv", index=False)
print(tabela.groupby("ponto")[["VV", "S1_SoilMoisture"]].describe().round(2))
print("Imagens por ponto:", tabela.groupby("ponto").size().to_dict())
