
namespace Sistema_Classificacao_Seca.Model
{
    public class SoilObservation
    {
        public int Id { get; set; }

        public int RegionId { get; set; }

        public Region Region { get; set; } = null!;

        public DateTime Date { get; set; }

        public float Sentinel1Moisture { get; set; }

        public float AscatMoisture { get; set; }

        public float Clay { get; set; }

        public float Sand { get; set; }

        public float OrganicCarbon { get; set; }

        public string DroughtClass { get; set; } = string.Empty;

        // Novas propriedades do dataset

        public string Point { get; set; } = string.Empty;

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public float S1_VV_dB { get; set; }

        public float Silt { get; set; }

        public float BulkDensity { get; set; }

        public float SM_Percentile { get; set; }
    }
}
