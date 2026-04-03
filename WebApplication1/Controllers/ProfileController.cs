using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.DTO;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly SteamService _steamService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(
            ApplicationDbContext db,
            SteamService steamService,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _steamService = steamService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
                return RedirectToAction("Login", "Account");

            return RedirectToAction(nameof(UserProfile), new { steamId = identityUser.SteamId });
        }

        [HttpGet]
        public async Task<IActionResult> UserProfile(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return NotFound();

            var user = await _db.Users
                .Include(x => x.Achievements)
                    .ThenInclude(x => x.Achievement)
                        .ThenInclude(a => a.Game) 
                .FirstOrDefaultAsync(x => x.SteamId == steamId);

            if (user == null)
                return NotFound("Профиль пользователя не найден");

            var identityUser = await _userManager.GetUserAsync(User);
            var currentSteamId = identityUser?.SteamId;
            var isOwner = currentSteamId == user.SteamId;

            if (isOwner)
            {
                var steamProfile = await _steamService.GetProfileAsync(user.SteamId);
                if (steamProfile != null)
                {
                    if (!string.IsNullOrWhiteSpace(steamProfile.Personaname))
                        user.SteamName = steamProfile.Personaname;

                    if (!string.IsNullOrWhiteSpace(steamProfile.Avatarfull))
                        user.AvatarID = steamProfile.Avatarfull;

                    await _db.SaveChangesAsync();
                }

                if (user.LastSync == DateTime.MinValue || (DateTime.UtcNow - user.LastSync).TotalMinutes > 10)
                    await SyncAchievementsForUserAsync(user);
            }

            var orderedUsers = await _db.Users
                .OrderByDescending(x => x.TotalAchievements)
                .ThenBy(x => x.SteamName)
                .Select(x => new { x.Id })
                .ToListAsync();

            var rank = orderedUsers.FindIndex(x => x.Id == user.Id) + 1;

            var gamesCount = await _db.UserAchievements
                .Where(x => x.UserId == user.Id && x.Completed)
                .Select(x => x.Achievement.GameId)
                .Distinct()
                .CountAsync();

            ViewBag.Rank = rank > 0 ? rank : 1;
            ViewBag.GamesCount = gamesCount;
            ViewBag.IsOwner = isOwner;
            var recentAchievements = user.Achievements
                .Where(a => a.Completed && a.UnlockTime != null)
                .OrderByDescending(a => a.UnlockTime)
                .Take(5)
                .ToList();

            ViewBag.RecentAchievements = recentAchievements;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sync()
        {
            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
                return RedirectToAction("Login", "Account");

            var steamId = identityUser.SteamId;

            var user = await _db.Users.FirstOrDefaultAsync(x => x.SteamId == steamId);
            if (user == null)
                return NotFound("Пользователь не найден");

            await SyncAchievementsForUserAsync(user);

            return RedirectToAction(nameof(UserProfile), new { steamId });
        }

        private async Task SyncAchievementsForUserAsync(User user)
        {
            var games = await _db.Games
                .Include(g => g.Achievements)
                .Where(g => g.SteamAppId > 0)
                .ToListAsync();

            foreach (var game in games)
            {
                List<SteamPlayerAchievement> steamAchievements;

                try
                {
                    steamAchievements = await _steamService.GetPlayerAchievements(user.SteamId, game.SteamAppId);
                }
                catch
                {
                    continue;
                }

                foreach (var steamAch in steamAchievements)
                {
                    var dbAchievement = game.Achievements.FirstOrDefault(a => a.ApiName == steamAch.ApiName);
                    if (dbAchievement == null)
                        continue;

                    var userAchievement = await _db.UserAchievements
                        .FirstOrDefaultAsync(ua => ua.UserId == user.Id && ua.AchievementId == dbAchievement.Id);

                    if (userAchievement == null)
                    {
                        userAchievement = new UserAchievement
                        {
                            UserId = user.Id,
                            AchievementId = dbAchievement.Id,
                            Completed = steamAch.Achieved,
                            UnlockTime = steamAch.Achieved ? DateTime.UtcNow : null
                        };

                        _db.UserAchievements.Add(userAchievement);
                    }
                    else
                    {
                        userAchievement.Completed = steamAch.Achieved;

                        if (steamAch.Achieved && userAchievement.UnlockTime == null)
                            userAchievement.UnlockTime = DateTime.UtcNow;
                    }
                }
            }

            await _db.SaveChangesAsync();

            user.TotalAchievements = await _db.UserAchievements
                .CountAsync(x => x.UserId == user.Id && x.Completed);

            user.LastSync = DateTime.UtcNow;
            await _db.SaveChangesAsync();

        }
    }
}