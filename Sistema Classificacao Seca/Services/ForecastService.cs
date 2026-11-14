using Sistema_Classificacao_Seca.Model;

namespace Sistema_Classificacao_Seca.Services;

public sealed record ForecastPoint(
    int RegionId,
    string RegionName,
    int HorizonMonths,
    DateTime ForecastDate,
    double ExpectedMoisture,
    double RiskScore,
    string RiskLevel,
    string DroughtClass,
    double Trend,
    string Summary);

public sealed class ForecastService
{
    public static IReadOnlyList<ForecastPoint> Generate(IReadOnlyList<SoilObservation> observations, int regionId, int[] months)
    {
        if (observations.Count == 0)
            return [];

        var regionRows = observations
            .Where(row => row.RegionId == regionId)
            .OrderBy(row => row.Date)
            .ToList();

        if (regionRows.Count == 0)
            return [];

        var regionName = regionRows.First().Region?.Name ?? $"Região {regionId}";
        var latestDate = regionRows.Max(row => row.Date);
        var monthlySeries = regionRows
            .GroupBy(row => new DateTime(row.Date.Year, row.Date.Month, 1))
            .Select(group => new MonthlyObservation(
                group.Key,
                group.Average(row => (row.Sentinel1Moisture * 100.0 + row.AscatMoisture) / 2.0),
                group.Average(row => row.SM_Percentile)))
            .OrderBy(item => item.Date)
            .ToList();

        var recent = monthlySeries.TakeLast(12).ToList();
        var moistureTrend = CalculateTrend(recent.Select(item => item.Moisture).ToArray());
        var percentileTrend = CalculateTrend(recent.Select(item => item.Percentile).ToArray());
        var latest = monthlySeries[^1];
        var moistureSeasonality = monthlySeries.GroupBy(item => item.Date.Month)
            .ToDictionary(group => group.Key, group => group.Average(item => item.Moisture));
        var percentileSeasonality = monthlySeries.GroupBy(item => item.Date.Month)
            .ToDictionary(group => group.Key, group => group.Average(item => item.Percentile));

        var projections = new List<ForecastPoint>();

        foreach (var horizon in months.Distinct().OrderBy(month => month))
        {
            var forecastDate = latestDate.AddMonths(horizon);
            var monthKey = forecastDate.Month;
            var moistureBaseline = moistureSeasonality[monthKey];
            var percentileBaseline = percentileSeasonality[monthKey];
            var persistence = Math.Pow(0.65, horizon);
            var trendDamping = 1d / (1d + horizon / 6d);
            var expectedMoisture = Math.Clamp(
                moistureBaseline + (latest.Moisture - moistureSeasonality[latest.Date.Month]) * persistence
                    + moistureTrend * horizon * trendDamping,
                0d, 100d);
            var expectedPercentile = Math.Clamp(
                percentileBaseline + (latest.Percentile - percentileSeasonality[latest.Date.Month]) * persistence
                    + percentileTrend * horizon * trendDamping,
                0d, 100d);

            var droughtClass = expectedPercentile switch
            {
                <= 5 => "Extrema",
                <= 10 => "Severa",
                <= 20 => "Moderada",
                _ => "Normal"
            };

            var riskScore = 100d - expectedPercentile;
            var riskLevel = droughtClass switch
            {
                "Extrema" => "Crítico",
                "Severa" => "Alto",
                "Moderada" => "Moderado",
                _ => "Baixo"
            };

            var summary = droughtClass switch
            {
                "Extrema" => "A série histórica indica condições compatíveis com seca extrema; requer atenção prioritária.",
                "Severa" => "A tendência histórica aponta risco elevado de seca; acompanhar a região de perto.",
                "Moderada" => "Há sinal de défice de humidade; recomenda-se monitorização regular.",
                _ => "O padrão sazonal histórico indica risco reduzido de seca para este período."
            };

            projections.Add(new ForecastPoint(
                regionId,
                regionName,
                horizon,
                forecastDate,
                expectedMoisture,
                riskScore,
                riskLevel,
                droughtClass,
                moistureTrend,
                summary));
        }

        return projections;
    }

    private static double CalculateTrend(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
            return 0d;

        var meanX = (values.Count - 1) / 2d;
        var meanY = values.Average();
        var numerator = 0d;
        var denominator = 0d;
        for (var index = 0; index < values.Count; index++)
        {
            var deltaX = index - meanX;
            numerator += deltaX * (values[index] - meanY);
            denominator += deltaX * deltaX;
        }

        return denominator == 0d ? 0d : numerator / denominator;
    }

    private sealed record MonthlyObservation(DateTime Date, double Moisture, double Percentile);
}
