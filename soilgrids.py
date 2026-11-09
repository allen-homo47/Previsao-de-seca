import time
from pathlib import Path

import pandas as pd
import requests

URL = "https://rest.isric.org/soilgrids/v2.0/properties/query"
PROPRIEDADES = ["clay", "sand", "silt", "soc", "bdod"]

# Pontos em zonas agrícolas (fora das vilas, onde o SoilGrids não tem dados)
PONTOS = [
    {"regiao": "Chokwe", "ponto": "CHK_01", "lat": -24.45, "lon": 33.05},
    {"regiao": "Chokwe", "ponto": "CHK_02", "lat": -24.55, "lon": 33.10},
    {"regiao": "Massinga", "ponto": "MAS_01", "lat": -23.25, "lon": 35.35},
    {"regiao": "Massinga", "ponto": "MAS_02", "lat": -23.40, "lon": 35.30},
]


def recolher_ponto(lat, lon):
    """Pede ao SoilGrids as propriedades do solo de um ponto e devolve um dicionário."""
    parametros = [("lat", lat), ("lon", lon), ("depth", "0-5cm"), ("value", "mean")]
    for p in PROPRIEDADES:
        parametros.append(("property", p))

    resposta = requests.get(URL, params=parametros, timeout=60)
    resposta.raise_for_status()  # pára com erro se o código HTTP não for 200

    resultado = {}
    for camada in resposta.json()["properties"]["layers"]:
        valor = camada["depths"][0]["values"]["mean"]
        fator = camada["unit_measure"]["d_factor"]
        if valor is None:
            resultado[camada["name"]] = None
        else:
            resultado[camada["name"]] = valor / fator
    return resultado


linhas = []
for ponto in PONTOS:
    print("A recolher", ponto["ponto"], "...")
    solo = recolher_ponto(ponto["lat"], ponto["lon"])
    linhas.append({**ponto, **solo})
    time.sleep(15)  # o SoilGrids só aceita ~5 pedidos por minuto

tabela = pd.DataFrame(linhas)
print(tabela)

pasta = Path(__file__).parent / "dados" / "processados"
tabela.to_csv(pasta / "soilgrids.csv", index=False)
print("Guardado em", pasta / "soilgrids.csv")
