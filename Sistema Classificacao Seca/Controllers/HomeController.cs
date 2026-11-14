using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Sistema_Classificacao_Seca.Data;

namespace Sistema_Classificacao_Seca.Controllers
{
    public sealed record HomeDashboard(int ObservationCount, int RegionCount,
        DateTime? LatestObservationDate, IReadOnlyList<HomeRegionSummary> Regions,
        IReadOnlyList<HomeClassSummary> DroughtClasses);
    public sealed record HomeRegionSummary(string Name, string Province, int ObservationCount,
        DateTime LatestObservationDate);
    public sealed record HomeClassSummary(string Name, int Count);

    public class HomeController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var regionRows = await db.SoilObservations
                .AsNoTracking()
                .GroupBy(row => new { row.RegionId, row.Region.Name, row.Region.Province })
                .Select(group => new
                {
                    group.Key.Name,
                    group.Key.Province,
                    ObservationCount = group.Count(),
                    LatestObservationDate = group.Max(row => row.Date)
                })
                .OrderByDescending(region => region.ObservationCount)
                .ThenBy(region => region.Name)
                .ToListAsync(ct);

            var regions = regionRows.Select(region => new HomeRegionSummary(
                region.Name, region.Province, region.ObservationCount, region.LatestObservationDate)).ToList();

            var droughtClasses = await db.SoilObservations
                .AsNoTracking()
                .GroupBy(row => row.DroughtClass)
                .Select(group => new HomeClassSummary(group.Key, group.Count()))
                .OrderByDescending(item => item.Count)
                .ToListAsync(ct);

            var dashboard = new HomeDashboard(
                regions.Sum(region => region.ObservationCount),
                regions.Count,
                regions.Count == 0 ? null : regions.Max(region => region.LatestObservationDate),
                regions,
                droughtClasses);

            return View(dashboard);
        }
    }
}
