using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly SteamService _steamService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CacheService _cacheService;

        public ProfileController(
            ApplicationDbContext db,
            SteamService steamService,
            UserManager<ApplicationUser> userManager,
            CacheService cacheService)
        {
            _db = db;
            _steamService = steamService;
            _userManager = userManager;
            _cacheService = cacheService;
        }

        private static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "";

            return s.Trim().ToLowerInvariant();
        }

        public async Task<IActionResult> Index()
        {
            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
                return RedirectToAction("Login", "Account");

            return RedirectToAction(nameof(UserProfile), new { steamId = identityUser.SteamId });
        }

        [HttpGet]
        public async Task<IActionResult> UserProfile(string steamId, int page = 1, int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.SteamId == steamId);
            if (user == null)
                return NotFound("Профиль пользователя не найден");

            var identityUser = await _userManager.GetUserAsync(User);
            var isOwner = identityUser?.SteamId == user.SteamId;

            if (!user.IsProfilePublic && !isOwner)
                return Forbid();

            if (isOwner)
            {
                var steamProfile = await _steamService.GetProfileAsync(user.SteamId);
                if (steamProfile != null)
                {
                    if (!string.IsNullOrWhiteSpace(steamProfile.Personaname))
                        user.SteamName = steamProfile.Personaname;

                    if (!string.IsNullOrWhiteSpace(steamProfile.Avatarfull))
                        user.AvatarID = steamProfile.Avatarfull;
                }

                if (!user.LastSync.HasValue || (DateTime.UtcNow - user.LastSync.Value).TotalMinutes > 5)
                    await SyncAchievementsForUserAsync(user.Id);
                else
                    await _db.SaveChangesAsync();

                // перечитываем пользователя после синка, чтобы вьюха видела актуальные цифры
                user = await _db.Users.FirstAsync(u => u.Id == user.Id);
            }

            var safePage = page < 1 ? 1 : page;
            var safePageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

            var achievementsQuery = _db.UserAchievements
                .Where(x => x.UserId == user.Id && x.Completed)
                .Include(x => x.Achievement)
                    .ThenInclude(a => a.Game)
                .OrderByDescending(x => x.UnlockTime)
                .AsNoTracking();

            ViewBag.RecentAchievements = await achievementsQuery.Take(5).ToListAsync();

            ViewBag.RareAchievements = await achievementsQuery
                .Where(x => x.Achievement.GlobalUnlockRate > 0 && x.Achievement.GlobalUnlockRate < 10)
                .OrderBy(x => x.Achievement.GlobalUnlockRate)
                .Take(6)
                .ToListAsync();

            var total = await achievementsQuery.CountAsync();
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)safePageSize));
            ViewBag.CurrentPage = safePage;

            var pagedAchievements = await achievementsQuery
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .ToListAsync();

            ViewBag.PagedAchievements = pagedAchievements;
            ViewBag.LegendaryAchievements = pagedAchievements
                .Where(x => x.Achievement.GlobalUnlockRate > 0 && x.Achievement.GlobalUnlockRate < 1)
                .ToList();
            ViewBag.EpicAchievements = pagedAchievements
                .Where(x => x.Achievement.GlobalUnlockRate >= 1 && x.Achievement.GlobalUnlockRate < 5)
                .ToList();
            ViewBag.RareGroupedAchievements = pagedAchievements
                .Where(x => x.Achievement.GlobalUnlockRate >= 5 && x.Achievement.GlobalUnlockRate < 10)
                .ToList();
            ViewBag.CommonAchievements = pagedAchievements
                .Where(x => x.Achievement.GlobalUnlockRate >= 10 || x.Achievement.GlobalUnlockRate <= 0)
                .ToList();

            ViewBag.GamesCount = await _db.UserAchievements
                .Where(x => x.UserId == user.Id && x.Completed)
                .Select(x => x.Achievement.GameId)
                .Distinct()
                .CountAsync();

            var rank = await _db.Users.CountAsync(u => u.TotalAchievements > user.TotalAchievements);
            ViewBag.Rank = rank + 1;
            ViewBag.IsOwner = isOwner;

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePrivacy()
        {
            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser == null)
                return RedirectToAction("Login", "Account");

            var user = await _db.Users.FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);
            if (user == null)
                return NotFound();

            user.IsProfilePublic = !user.IsProfilePublic;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(UserProfile), new { steamId = user.SteamId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sync()
        {
            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
                return RedirectToAction("Login", "Account");

            var user = await _db.Users.FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);
            if (user == null)
                return NotFound();

            await SyncAchievementsForUserAsync(user.Id, force: true);
            return RedirectToAction(nameof(UserProfile), new { steamId = user.SteamId });
        }

        private async Task SyncAchievementsForUserAsync(int userId, bool force = false)
        {
            var dbUser = await _db.Users
                .Include(u => u.UserAchievements)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (dbUser == null)
                return;

            if (!force && dbUser.LastSync.HasValue && (DateTime.UtcNow - dbUser.LastSync.Value).TotalMinutes <= 5)
                return;

            var ownedGames = await _steamService.GetOwnedGames(dbUser.SteamId);
            var gamesToProcess = ownedGames
                .Where(g => g.PlaytimeForever > 0)
                .OrderByDescending(g => g.PlaytimeForever)
                .Take(50)
                .ToList();

            foreach (var ownedGame in gamesToProcess)
            {
                await SyncSingleGameAsync(dbUser.Id, dbUser.SteamId, ownedGame.AppId);
            }

            dbUser.TotalAchievements = await _db.UserAchievements
                .CountAsync(x => x.UserId == dbUser.Id && x.Completed);

            dbUser.LastSync = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        private async Task SyncSingleGameAsync(int userId, string steamId, int appId)
        {
            var game = await _db.Games
                .Include(g => g.Achievements)
                .FirstOrDefaultAsync(g => g.SteamAppId == appId);

            if (game == null || game.Achievements.Count == 0)
            {
                var gameDataTask = _steamService.GetGameDataAsync(appId);
                var schemaTask = _steamService.GetAchievementsAsync(appId);
                var ratesTask = _cacheService.GetOrCreateGlobalRates(appId);

                await Task.WhenAll(gameDataTask, schemaTask, ratesTask);

                var gameData = await gameDataTask;
                var schemaAchievements = await schemaTask;
                var globalRates = await ratesTask;

                if (schemaAchievements.Count == 0)
                    return;

                if (game == null)
                {
                    game = new Game
                    {
                        SteamAppId = appId,
                        Name = gameData?.Name ?? "",
                        AvatarUrl = gameData?.HeaderImage ?? "",
                        Description = gameData?.ShortDescription ?? "",
                        Achievements = new List<Achievement>()
                    };
                    _db.Games.Add(game);
                }

                foreach (var schemaAchievement in schemaAchievements)
                {
                    var normalizedApiName = Normalize(schemaAchievement.Name);

                    var existingAchievement = game.Achievements
                        .FirstOrDefault(a => Normalize(a.ApiName) == normalizedApiName);

                    if (existingAchievement != null)
                    {
                        if (globalRates.TryGetValue(normalizedApiName, out var existingPercent))
                            existingAchievement.GlobalUnlockRate = existingPercent;
                        continue;
                    }

                    game.Achievements.Add(new Achievement
                    {
                        Title = schemaAchievement.DisplayName ?? "",
                        Description = schemaAchievement.Description ?? "",
                        ApiName = schemaAchievement.Name ?? "",
                        GlobalUnlockRate = globalRates.TryGetValue(normalizedApiName, out var percent) ? percent : 0
                    });
                }

                await _db.SaveChangesAsync();
            }
            else
            {
                var globalRates = await _cacheService.GetOrCreateGlobalRates(appId);
                var changed = false;

                foreach (var achievement in game.Achievements)
                {
                    var normalizedApiName = Normalize(achievement.ApiName);
                    if (globalRates.TryGetValue(normalizedApiName, out var percent) && achievement.GlobalUnlockRate != percent)
                    {
                        achievement.GlobalUnlockRate = percent;
                        changed = true;
                    }
                }

                if (changed)
                    await _db.SaveChangesAsync();
            }

            var playerAchievements = await _steamService.GetPlayerAchievements(steamId, appId);
            if (playerAchievements.Count == 0)
                return;

            var completedMap = playerAchievements
                .Where(x => !string.IsNullOrWhiteSpace(x.ApiName))
                .GroupBy(x => Normalize(x.ApiName))
                .ToDictionary(g => g.Key, g => g.Any(x => x.Achieved));

            var achievementIds = game.Achievements.Select(a => a.Id).ToList();
            var existingUserAchievements = await _db.UserAchievements
                .Where(x => x.UserId == userId && achievementIds.Contains(x.AchievementId))
                .ToDictionaryAsync(x => x.AchievementId);

            foreach (var achievement in game.Achievements)
            {
                var normalizedApiName = Normalize(achievement.ApiName);
                var completed = completedMap.TryGetValue(normalizedApiName, out var done) && done;

                if (existingUserAchievements.TryGetValue(achievement.Id, out var existingUserAchievement))
                {
                    existingUserAchievement.Completed = completed;

                    if (completed && existingUserAchievement.UnlockTime == null)
                        existingUserAchievement.UnlockTime = DateTime.UtcNow;
                }
                else
                {
                    _db.UserAchievements.Add(new UserAchievement
                    {
                        UserId = userId,
                        AchievementId = achievement.Id,
                        Completed = completed,
                        UnlockTime = completed ? DateTime.UtcNow : null
                    });
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}
