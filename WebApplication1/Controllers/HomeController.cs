using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Catalog()
        {
            return View();
        }
        public IActionResult Leaderboard()
        {
            return View();
        }
        public IActionResult Challenges()
        {
            return View();
        }
    }
}
