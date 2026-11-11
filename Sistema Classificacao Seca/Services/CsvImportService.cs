using System.Data;
using System.Globalization;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Sistema_Classificacao_Seca.Data;
using Sistema_Classificacao_Seca.Model;

namespace Sistema_Classificacao_Seca.Services;

public sealed class CsvImportService(ApplicationDbContext db)
{
    private static readonly Dictionary<string, string> Provinces = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Chokwe"] = "Gaza",
        ["Massinga"] = "Inhambane"
    };

    public static async Task<List<SoilObservation>> ReadAsync(TextReader reader, CancellationToken ct)
    {
        using var csv = CsvReader.Create(reader);
        var linha = 1;
        var observations = new List<SoilObservation>();
        var keys = new HashSet<(string, string, DateTime)>();
        await foreach (var row in csv.GetRecordsAsync<SoilObservationCsv>(ct))
        {
            linha++;
            var region = Provinces.Keys.FirstOrDefault(name =>
                string.Equals(name, row.Region.Trim(), StringComparison.OrdinalIgnoreCase));
            var point = row.Point.Trim().ToUpperInvariant();
            var label = row.SM_Percentile switch
            {
                <= 5 => "Extrema",
                <= 10 => "Severa",
                <= 20 => "Moderada",
                _ => "Normal"
            };
            if (region is null || point.Length == 0 ||
                !DateTime.TryParseExact(row.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date) ||
                !double.IsFinite(row.Latitude) || Math.Abs(row.Latitude) > 90 ||
                !double.IsFinite(row.Longitude) || Math.Abs(row.Longitude) > 180 ||
                !new[] { row.Sentinel1Moisture, row.S1_VV_dB, row.AscatMoisture, row.Clay,
                    row.Sand, row.Silt, row.OrganicCarbon, row.BulkDensity, row.SM_Percentile }.All(float.IsFinite) ||
                row.Sentinel1Moisture is < 0 or > 1 || row.AscatMoisture is < 0 or > 100 ||
                row.Clay is < 0 or > 100 || row.Sand is < 0 or > 100 || row.Silt is < 0 or > 100 ||
                row.OrganicCarbon < 0 || row.BulkDensity <= 0 || row.SM_Percentile is < 0 or > 100 ||
                label != row.DroughtClass || !keys.Add((region, point, date)))
                throw new InvalidDataException($"Dados invÃ¡lidos ou duplicados na linha {linha}.");

            observations.Add(new SoilObservation
            {
                Region = new Region { Name = region, Province = Provinces[region] },
                Point = point, Date = date, Latitude = row.Latitude, Longitude = row.Longitude,
                Sentinel1Moisture = row.Sentinel1Moisture, S1_VV_dB = row.S1_VV_dB,
                AscatMoisture = row.AscatMoisture, Clay = row.Clay, Sand = row.Sand,
                Silt = row.Silt, OrganicCarbon = row.OrganicCarbon, BulkDensity = row.BulkDensity,
                SM_Percentile = row.SM_Percentile, DroughtClass = label
            });
        }

        if (observations.Count == 0)
            throw new InvalidDataException("O CSV nÃ£o contÃ©m observaÃ§Ãµes.");

        return observations;
    }

    public async Task<int> ImportAsync(IReadOnlyList<SoilObservation> rows, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var regions = (await db.Regions.OrderBy(r => r.Id).ToListAsync(ct))
            .GroupBy(r => r.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var existing = await db.SoilObservations
            .Select(o => new { o.Region.Name, o.Point, o.Date }).ToListAsync(ct);
        var keys = existing.Select(o => (o.Name.Trim().ToUpperInvariant(),
            o.Point.Trim().ToUpperInvariant(), o.Date.Date)).ToHashSet();

        var inserted = 0;
        foreach (var row in rows)
        {
            var name = row.Region.Name;
            if (!keys.Add((name.ToUpperInvariant(), row.Point, row.Date)))
                continue;
            if (regions.TryGetValue(name, out var region))
                row.Region = region;
            else
            {
                db.Regions.Add(row.Region);
                regions.Add(name, row.Region);
            }

            db.SoilObservations.Add(row);
            inserted++;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return inserted;
    }
}
