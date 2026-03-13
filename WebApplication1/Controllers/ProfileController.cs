using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplication1.DTO;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly SteamService _steamService;

        public ProfileController(ApplicationDbContext db, SteamService steamService)
        {
            _db = db;
            _steamService = steamService;
        }

        public IActionResult Index()
        {
            var rawSteamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(rawSteamId))
                return RedirectToAction("Login", "Account");

            var steamId = rawSteamId.Contains("/") ? rawSteamId.Split('/').Last() : rawSteamId;
            return RedirectToAction(nameof(UserProfile), new { steamId });
        }

        [HttpGet]
        public async Task<IActionResult> UserProfile(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return NotFound();

            var user = await _db.Users
                .Include(x => x.Achievements)
                .ThenInclude(x => x.Achievement)
                .FirstOrDefaultAsync(x => x.SteamId == steamId);

            if (user == null)
                return NotFound("Профиль пользователя не найден");

            var currentSteamIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var currentSteamId = string.IsNullOrWhiteSpace(currentSteamIdRaw)
                ? null
                : (currentSteamIdRaw.Contains("/") ? currentSteamIdRaw.Split('/').Last() : currentSteamIdRaw);

            var isOwner = currentSteamId == user.SteamId;

            if (isOwner)
            {
                var steamProfile = await _steamService.GetProfileAsync(user.SteamId);
                if (steamProfile != null)
                {
                    user.SteamName = string.IsNullOrWhiteSpace(steamProfile.Personaname) ? user.SteamName : steamProfile.Personaname;
                    user.AvatarID = string.IsNullOrWhiteSpace(steamProfile.Avatarfull) ? user.AvatarID : steamProfile.Avatarfull;
                    await _db.SaveChangesAsync();
                }

                if (user.LastSync == DateTime.MinValue || (DateTime.UtcNow - user.LastSync).TotalMinutes > 10)
                {
                    await SyncAchievementsForUserAsync(user);
                }
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

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sync()
        {
            var rawSteamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(rawSteamId))
                return RedirectToAction("Login", "Account");

            var steamId = rawSteamId.Contains("/") ? rawSteamId.Split('/').Last() : rawSteamId;

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

            var totalMatched = 0;
            var totalCompleted = 0;
            var errors = new List<string>();

            foreach (var game in games)
            {
                List<SteamPlayerAchievement> steamAchievements;

                try
                {
                    steamAchievements = await _steamService.GetPlayerAchievements(user.SteamId, game.SteamAppId);
                }
                catch (Exception ex)
                {
                    errors.Add($"{game.Name}: {ex.Message}");
                    continue;
                }

                if (steamAchievements.Count == 0)
                    continue;

                foreach (var steamAch in steamAchievements)
                {
                    var dbAchievement = game.Achievements.FirstOrDefault(a => a.ApiName == steamAch.ApiName);
                    if (dbAchievement == null)
                        continue;

                    totalMatched++;

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

                    if (steamAch.Achieved)
                        totalCompleted++;
                }
            }

            await _db.SaveChangesAsync();

            user.TotalAchievements = await _db.UserAchievements
                .CountAsync(x => x.UserId == user.Id && x.Completed);

            user.LastSync = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            if (errors.Count > 0 && user.TotalAchievements == 0)
            {
                TempData["SyncError"] = "Steam не отдал достижения хотя бы по части игр. Проверь открытость профиля и наличие локально добавленных игр.";
            }
            else if (totalMatched == 0)
            {
                TempData["SyncError"] = "Не найдено совпадений между локальными ачивками и данными Steam. Заново добавь игры в каталог после очистки БД.";
            }
        }
    }
}