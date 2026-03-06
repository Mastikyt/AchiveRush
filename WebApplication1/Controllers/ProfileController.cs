using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly SteamSyncService _sync;

        public ProfileController(ApplicationDbContext db, SteamSyncService sync)
        {
            _db = db;
            _sync = sync;
        }

        public async Task<IActionResult> Index()
        {
            var steamId = User.FindFirst("SteamId")?.Value;

            await _sync.SyncUser(steamId);

            var user = await _db.Users
                .FirstOrDefaultAsync(x => x.SteamId == steamId);

            var rank = _db.Users
                .OrderByDescending(x => x.TotalAchievements)
                .ToList()
                .FindIndex(x => x.Id == user.Id) + 1;

            return View(user);
        }
    }
}
