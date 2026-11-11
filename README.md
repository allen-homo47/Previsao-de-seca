# Seca Grupo 1 — Recolha de dados

Recolha e preparação dos dados do **Grupo 1 — AI Soil Moisture Drought Classification System** (Chókwè e Massinga, Moçambique), da disciplina de Programação Web Avançada com .NET.

O resultado final é `dados/processados/tabela_treino.csv`, usado para treinar o modelo de classificação de seca no ML.NET.

## Fontes de dados

| Fonte | O que dá | Acesso | Script |
|---|---|---|---|
| [SoilGrids 2.0](https://rest.isric.org/soilgrids/v2.0/docs) (ISRIC) | Argila, areia, silte, carbono orgânico e densidade (0–5 cm) | API pública, sem conta | `soilgrids.py` |
| Sentinel-1 GRD (Copernicus) | Backscatter de radar VV/VH, órbita relativa 6 (descendente) | Google Earth Engine (`COPERNICUS/S1_GRD`) | `sentinel1.py` |
| ASCAT — C3S Soil Moisture, sensor *active*, v202505 | Humidade superficial (% de saturação), médias de 10 dias | [Copernicus Climate Data Store](https://cds.climate.copernicus.eu/datasets/satellite-soil-moisture) | `ascat.py` |

Período: 2019–2024. Pontos: 2 em Chókwè e 2 em Massinga, em zona agrícola (o SoilGrids não tem dados em zonas urbanas).

## Como reproduzir

1. Python 3.13 e as dependências:
   ```
   pip install -r requirements.txt
   ```
2. Contas e credenciais:
   - **Google Earth Engine**: projecto Cloud registado para uso não comercial; autenticar com `earthengine authenticate`. O ID do projecto está em `sentinel1.py` (`ee.Initialize(project=...)`).
   - **Copernicus CDS**: ficheiro `~/.cdsapirc` com `url` e `key`, e licença do dataset *satellite-soil-moisture* aceite no site. A chave nunca deve ir para o repositório.
3. Correr os scripts por esta ordem:
   ```
   python soilgrids.py
   python sentinel1.py
   python ascat.py
   python juntar.py
   ```

Os ficheiros brutos do ASCAT (~240 MB) não estão no repositório; o `ascat.py` descarrega-os para `dados/brutos/ascat/`.

## Tratamento dos dados

- **Sentinel-1 → índice de humidade** (`S1_SoilMoisture`): por *change detection*, em cada ponto, `(VV − p5) / (p95 − p5)`, limitado a [0, 1]. É um índice relativo (0 = mais seco observado no ponto, 1 = mais húmido), não humidade em m³/m³.
- **ASCAT**: para cada ponto usa-se a célula (0,25°) mais próxima com dados em pelo menos metade das datas; as células junto à costa vêm vazias.
- **Alinhamento temporal**: tudo agregado em décadas (dias 1, 11 e 21). Ficam só as décadas com Sentinel-1 e ASCAT.
- **Solo**: valores fixos por ponto, repetidos em todas as datas.

## Classe de seca (`DroughtClass`)

1. Para cada ponto e fonte, anomalia padronizada face ao mesmo mês nos outros anos: `(valor − média do mês) / desvio do mês`.
2. Média das anomalias do Sentinel-1 e do ASCAT.
3. Percentil dessa anomalia entre todas as datas do ponto (`SM_Percentile`).
4. Limiares inspirados no US Drought Monitor:

| Percentil | Classe |
|---|---|
| < 5 | Extrema |
| 5–10 | Severa |
| 10–20 | Moderada |
| > 20 | Normal |

Distribuição obtida: Normal 626, Moderada 80, Severa 40, Extrema 36 (782 linhas).

## Limitações

- A classe é derivada da própria humidade, por isso um modelo que use a humidade como entrada tende a reaprender a regra. `SM_Percentile` não deve ser usado como entrada do modelo.
- Classes desequilibradas (~80% Normal): avaliar com F1-score e matriz de confusão, não só Accuracy.
- O backscatter do Sentinel-1 também depende da vegetação e da rugosidade do solo.
- O Sentinel-1B deixou de funcionar em Dezembro de 2021, o que reduz o número de imagens a partir de 2022.
- O ASCAT tem resolução de ~25 km: serve como referência regional, não local.
- Apenas 4 pontos; para o mapa é preciso acrescentar mais pontos nos três scripts.

### ASP.NET Core · Tema 1

Na raiz do repositório, com o SDK .NET 10 e uma instância SQL Server disponível:

```powershell
dotnet build
dotnet run --project "Sistema Classificacao Seca" -- --validate-csv dados/processados/tabela_treino.csv
dotnet ef database update --project "Sistema Classificacao Seca"
dotnet run --project "Sistema Classificacao Seca" -- --import-csv dados/processados/tabela_treino.csv
dotnet run --project "Sistema Classificacao Seca" -- --evaluate-csv dados/processados/tabela_treino.csv
dotnet run --project "Sistema Classificacao Seca"
```

O comando `dotnet ef` requer a ferramenta dotnet-ef 10.x. Fora do Windows, configure
`ConnectionStrings__DefaultConnection` para uma instância SQL Server acessível.
A importação lê o CSV consolidado de Sentinel-1, ASCAT e SoilGrids. O comando valida
as 782 linhas antes de gravar e ignora chaves região/ponto/data já existentes.

- Dashboard e formulário em `/Drought`. O gráfico usa Chart.js via CDN.
- `GET /api/regions` lista as regiões.
- `GET /api/predictions?regionId=1&from=2023-09-01&to=2023-09-30` classifica os pontos observados no período.
- `POST /api/predictions` recebe JSON com `sentinel1Moisture`, `ascatMoisture`,
  `month`, `clay`, `sand`, `silt`, `organicCarbon` e `bulkDensity`.
- `GET /api/models/metrics` devolve as métricas dos dois classificadores.
- OpenAPI em `/openapi/v1.json` no ambiente de desenvolvimento.

O módulo compara SDCA Maximum Entropy e L-BFGS Maximum Entropy em ML.NET, com separação por data.
O SDCA é o modelo provisório para classificação. A avaliação mede a capacidade de reproduzir
os rótulos derivados em `juntar.py`. O percentil fica fora das features, mas os rótulos
foram calculados com estatísticas de toda a série, incluindo o período de teste. Por isso,
a avaliação tem carácter retrospectivo. A confiança apresentada é a pontuação do modelo.
