
using Microsoft.AspNetCore.Mvc;

namespace Sistema_Classificacao_Seca.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
