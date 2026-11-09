import ee

ee.Initialize(project="secagrupo1")

ponto = ee.Geometry.Point([33.05, -24.45])  # Chókwè (atenção: longitude primeiro!)

imagens = (
    ee.ImageCollection("COPERNICUS/S1_GRD")
    .filterBounds(ponto)
    .filterDate("2024-01-01", "2024-12-31")
)

print("Imagens Sentinel-1 sobre Chókwè em 2024:", imagens.size().getInfo())
