using System.Diagnostics;
using CMAP_SISTEMAS_MVC.Models;
using Microsoft.AspNetCore.Mvc;
using CMAP_SISTEMAS_MVC.Data;

namespace CMAP_SISTEMAS_MVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly Cmap54SistemasContext _db;

        public HomeController(ILogger<HomeController> logger, Cmap54SistemasContext db)
        {
            _logger = logger;
            _db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> TestDb2()
        {
            var ok = await _db.Database.CanConnectAsync();
            return Content(ok ? "✅ Conectado a BDSISTEMAS" : "❌ No conecta a BDSISTEMAS");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id?? HttpContext.TraceIdentifier });
        }
    }
}
