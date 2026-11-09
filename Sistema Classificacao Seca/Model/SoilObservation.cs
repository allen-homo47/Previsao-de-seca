
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
    }
}
