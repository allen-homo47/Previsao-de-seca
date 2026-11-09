
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sistema_Classificacao_Seca.Data;

namespace Sistema_Classificacao_Seca.Controllers
{
    public class RegionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RegionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var regions = await _context.Regions
                .AsNoTracking()
                .OrderBy(r => r.Name)
                .ToListAsync();

            return View(regions);
        }
    }
}
