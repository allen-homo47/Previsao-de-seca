from pathlib import Path

import numpy as np
import pandas as pd

PASTA = Path(__file__).parent / "dados" / "processados"

# Limiares de percentil da humidade (inspirados no US Drought Monitor):
# abaixo de 5% -> Extrema, 5-10% -> Severa, 10-20% -> Moderada, acima de 20% -> Normal
LIMIARES = [0, 5, 10, 20, 100]
CLASSES = ["Extrema", "Severa", "Moderada", "Normal"]


def inicio_da_decada(datas):
    """Converte cada data para o 1.º dia da sua década (dia 1, 11 ou 21), como no ASCAT."""
    dia = np.select([datas.dt.day < 11, datas.dt.day < 21], [1, 11], 21)
    return datas.dt.to_period("M").dt.to_timestamp() + pd.to_timedelta(dia - 1, unit="D")


solo = pd.read_csv(PASTA / "soilgrids.csv")
s1 = pd.read_csv(PASTA / "sentinel1.csv", parse_dates=["data"])
ascat = pd.read_csv(PASTA / "ascat.csv", parse_dates=["data"])

# 1. Sentinel-1: pode haver mais de uma passagem na mesma década -> média
s1["data"] = inicio_da_decada(s1["data"])
s1 = s1.groupby(["regiao", "ponto", "data"], as_index=False)[["S1_SoilMoisture", "VV"]].mean()

# 2. Juntar Sentinel-1 + ASCAT pela mesma década e ponto (só ficam as décadas com as duas fontes)
tabela = s1.merge(ascat[["ponto", "data", "ASCAT_SM"]], on=["ponto", "data"], how="inner")

# 3. Acrescentar o solo (valores fixos por ponto, repetidos em todas as datas)
tabela = tabela.merge(solo[["ponto", "lat", "lon", "clay", "sand", "silt", "soc", "bdod"]], on="ponto")

# 4. Classe de seca, em dois passos:
#    a) anomalia padronizada: quanto cada valor se afasta da média desse mês, nesse ponto
#       (Agosto é sempre seco; seca é estar mais seco do que o habitual para a época)
#    b) percentil da anomalia (média das duas fontes) entre todas as datas do ponto
tabela["mes"] = tabela["data"].dt.month
grupo = tabela.groupby(["ponto", "mes"])
for coluna in ["S1_SoilMoisture", "ASCAT_SM"]:
    media = grupo[coluna].transform("mean")
    desvio = grupo[coluna].transform("std")
    tabela["anom_" + coluna] = (tabela[coluna] - media) / desvio
tabela["anomalia"] = (tabela["anom_S1_SoilMoisture"] + tabela["anom_ASCAT_SM"]) / 2
tabela["percentil"] = (tabela.groupby("ponto")["anomalia"].rank(pct=True) * 100).round(1)
tabela["DroughtClass"] = pd.cut(tabela["percentil"], bins=LIMIARES, labels=CLASSES, include_lowest=True)

final = tabela.rename(columns={
    "data": "Date", "regiao": "Region", "ponto": "Point", "lat": "Latitude", "lon": "Longitude",
    "VV": "S1_VV_dB", "clay": "Clay", "sand": "Sand", "silt": "Silt",
    "soc": "OrganicCarbon", "bdod": "BulkDensity", "percentil": "SM_Percentile",
})[[
    "Date", "Region", "Point", "Latitude", "Longitude", "S1_SoilMoisture", "S1_VV_dB", "ASCAT_SM",
    "Clay", "Sand", "Silt", "OrganicCarbon", "BulkDensity", "SM_Percentile", "DroughtClass",
]].sort_values(["Point", "Date"])

final.to_csv(PASTA / "tabela_treino.csv", index=False)
print(final.head(10).to_string(index=False))
print("\nLinhas:", len(final))
print("Classes:\n", final["DroughtClass"].value_counts().to_string())
print("Guardado em", PASTA / "tabela_treino.csv")
