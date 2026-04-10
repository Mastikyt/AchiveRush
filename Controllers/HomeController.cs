using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            return View();
        }

        public async Task<IActionResult> Catalog()
        {
            var games = await _context.Games
                .Include(g => g.Achievements)
                .ToListAsync();

            return View(games);
        }

        public async Task<IActionResult> Leaderboard()
        {
            var users = await _context.Users
                .Where(u => !string.IsNullOrEmpty(u.SteamId))
                .OrderByDescending(u => u.TotalAchievements)
                .ThenBy(u => u.SteamName)
                .ToListAsync();

            return View(users);
        }

        public IActionResult Challenges()
        {
            return View();
        }
    }
}