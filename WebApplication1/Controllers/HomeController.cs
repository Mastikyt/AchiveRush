using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                var steamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                ViewBag.SteamId = steamId;
            }

            return View();
        }

        public async Task<IActionResult> Catalog()
        {
            var games = await _context.Games
                .Include(g => g.Achievements)
                .ToListAsync();

            return View(games);
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
