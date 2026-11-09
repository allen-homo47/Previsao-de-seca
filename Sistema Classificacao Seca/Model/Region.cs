
namespace Sistema_Classificacao_Seca.Model
{
    public class Region
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Province { get; set; } = string.Empty;

        public ICollection<SoilObservation> SoilObservations
        { get; set; } = new List<SoilObservation>();
    }
}
