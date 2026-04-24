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
                .AsNoTracking()
                .OrderBy(g => g.Name)
                .Select(g => new WebApplication1.Models.Game
                {
                    Id = g.Id,
                    SteamAppId = g.SteamAppId,
                    Name = g.Name,
                    Description = g.Description,
                    AvatarUrl = g.AvatarUrl
                })
                .ToListAsync();

            var achievementCounts = await _context.Games
                .AsNoTracking()
                .Select(g => new
                {
                    g.Id,
                    Count = g.Achievements.Count()
                })
                .ToDictionaryAsync(x => x.Id, x => x.Count);

            ViewBag.AchievementCounts = achievementCounts;
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