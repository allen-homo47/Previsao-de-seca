
using CsvHelper;

namespace Sistema_Classificacao_Seca.Model
{
    [CsvRecord]
    public partial class SoilObservationCsv
    {
        [CsvColumn("Date")]
        public string Date { get; set; } = string.Empty;

        [CsvColumn("Region")]
        public string Region { get; set; } = string.Empty;

        [CsvColumn("Point")]
        public string Point { get; set; } = string.Empty;

        [CsvColumn("Latitude")]
        public double Latitude { get; set; }

        [CsvColumn("Longitude")]
        public double Longitude { get; set; }

        [CsvColumn("S1_SoilMoisture")]
        public float Sentinel1Moisture { get; set; }

        [CsvColumn("S1_VV_dB")]
        public float S1_VV_dB { get; set; }

        [CsvColumn("ASCAT_SM")]
        public float AscatMoisture { get; set; }

        [CsvColumn("Clay")]
        public float Clay { get; set; }

        [CsvColumn("Sand")]
        public float Sand { get; set; }

        [CsvColumn("Silt")]
        public float Silt { get; set; }

        [CsvColumn("OrganicCarbon")]
        public float OrganicCarbon { get; set; }

        [CsvColumn("BulkDensity")]
        public float BulkDensity { get; set; }
        


        [CsvColumn("SM_Percentile")]
        public float SM_Percentile { get; set; }

        [CsvColumn("DroughtClass")]
        public string DroughtClass { get; set; } = string.Empty;
    }
}
