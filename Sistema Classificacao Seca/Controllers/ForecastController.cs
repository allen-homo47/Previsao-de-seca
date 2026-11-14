using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema_Classificacao_Seca.Data;
using Sistema_Classificacao_Seca.Services;

namespace Sistema_Classificacao_Seca.Controllers;

public sealed record ForecastDashboard(int SelectedRegionId, IReadOnlyList<RegionSummary> Regions,
    IReadOnlyList<ForecastPoint> Forecasts, int HorizonMonths, DateTime? FirstObservationDate,
    DateTime? LatestObservationDate, int ObservationCount);

public sealed record RegionSummary(int Id, string Name, string Province, int ObservationCount);

public sealed class ForecastController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int regionId = 0, int months = 3, CancellationToken ct = default)
    {
        var regions = await db.Regions
            .AsNoTracking()
            .Select(region => new
            {
                region.Id,
                region.Name,
                region.Province,
                ObservationCount = region.SoilObservations.Count()
            })
            .Where(region => region.ObservationCount > 0)
            .OrderByDescending(region => region.ObservationCount)
            .ThenBy(region => region.Name)
            .ToListAsync(ct);

        var regionOptions = regions
            .Select(region => new RegionSummary(region.Id, region.Name, region.Province, region.ObservationCount))
            .ToList();

        if (regionOptions.Count == 0)
            return View(new ForecastDashboard(0, [], [], Math.Clamp(months, 1, 12), null, null, 0));

        var horizonMonths = Math.Clamp(months, 1, 12);
        var selectedRegionId = regionId > 0 && regionOptions.Any(region => region.Id == regionId)
            ? regionId
            : regionOptions[0].Id;
        var observations = await db.SoilObservations
            .AsNoTracking()
            .Include(row => row.Region)
            .Where(row => row.RegionId == selectedRegionId)
            .OrderBy(row => row.Date)
            .ToListAsync(ct);

        var forecasts = ForecastService.Generate(observations, selectedRegionId,
            Enumerable.Range(1, horizonMonths).ToArray());

        return View(new ForecastDashboard(selectedRegionId, regionOptions, forecasts, horizonMonths,
            observations.Count == 0 ? null : observations[0].Date,
            observations.Count == 0 ? null : observations[^1].Date,
            observations.Count));
    }
}
