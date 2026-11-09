
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema_Classificacao_Seca.Data;

namespace Sistema_Classificacao_Seca.Controllers
{
    public class SoilObservationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SoilObservationsController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? regionId)
        {
            var query = _context.SoilObservations
                .AsNoTracking()
                .Include(o => o.Region)
                .AsQueryable();

            if (regionId.HasValue)
            {
                query = query.Where(
                    o => o.RegionId == regionId.Value);
            }

            var observations = await query
                .OrderByDescending(o => o.Date)
                .ToListAsync();

            ViewBag.Regions = await _context.Regions
                .AsNoTracking()
                .OrderBy(r => r.Name)
                .ToListAsync();

            ViewBag.SelectedRegionId = regionId;

            return View(observations);
        }
    }
}
