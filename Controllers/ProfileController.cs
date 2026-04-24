using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.DTO;
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
        private const int SteamSyncConcurrency = 6;

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

        private static IQueryable<AchievementCardViewModel> ToAchievementCards(IQueryable<UserAchievement> query)
        {
            return query.Select(x => new AchievementCardViewModel
            {
                Id = x.Id,
                Title = x.Achievement.Title,
                Description = x.Achievement.Description,
                GameName = x.Achievement.Game.Name,
                GameAvatarUrl = x.Achievement.Game.AvatarUrl,
                IconUrl = x.IconUrl,
                UnlockTime = x.UnlockTime,
                GlobalUnlockRate = x.Achievement.GlobalUnlockRate
            });
        }

        public async Task<IActionResult> Index()
        {
            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
                return RedirectToAction("Login", "Account");

            return RedirectToAction(nameof(UserProfile), new { steamId = identityUser.SteamId });
        }

        [HttpGet]
        public async Task<IActionResult> UserProfile(string steamId, int page = 1, int pageSize = 24)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return NotFound();

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.SteamId == steamId);
            if (user == null)
                return NotFound("Профиль пользователя не найден");

            var identityUser = await _userManager.GetUserAsync(User);
            var isOwner = identityUser?.SteamId == user.SteamId;

            if (!user.IsProfilePublic && !isOwner)
                return Forbid();

            var profileAvatarUrl = !string.IsNullOrWhiteSpace(user.AvatarID)
                ? user.AvatarID
                : (!string.IsNullOrWhiteSpace(identityUser?.AvatarUrl) ? identityUser!.AvatarUrl : "/images/default_avatar.png");

            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 12, 60);

            var completedBaseQuery = _db.UserAchievements
                .AsNoTracking()
                .Where(x => x.UserId == user.Id && x.Completed);

            var stats = await completedBaseQuery
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalAchievements = g.Count(),
                    GamesCount = g.Select(x => x.Achievement.GameId).Distinct().Count(),
                    LegendaryCount = g.Count(x => x.Achievement.GlobalUnlockRate > 0 && x.Achievement.GlobalUnlockRate < 1),
                    EpicCount = g.Count(x => x.Achievement.GlobalUnlockRate >= 1 && x.Achievement.GlobalUnlockRate < 5),
                    RareCount = g.Count(x => x.Achievement.GlobalUnlockRate >= 5 && x.Achievement.GlobalUnlockRate < 10),
                    CommonCount = g.Count(x => x.Achievement.GlobalUnlockRate >= 10 || x.Achievement.GlobalUnlockRate <= 0)
                })
                .FirstOrDefaultAsync();

            var totalAchievements = stats?.TotalAchievements ?? 0;

            var recentAchievements = await ToAchievementCards(completedBaseQuery
                .OrderByDescending(x => x.UnlockTime)
                .ThenByDescending(x => x.Id)
                .Take(6))
                .ToListAsync();

            var rareAchievements = await ToAchievementCards(completedBaseQuery
                .Where(x => x.Achievement.GlobalUnlockRate > 0 && x.Achievement.GlobalUnlockRate < 10)
                .OrderBy(x => x.Achievement.GlobalUnlockRate)
                .ThenBy(x => x.Achievement.Title)
                .Take(6))
                .ToListAsync();

            var totalPages = Math.Max(1, (int)Math.Ceiling(totalAchievements / (double)safePageSize));
            var currentPage = Math.Min(safePage, totalPages);

            var pagedAchievements = await ToAchievementCards(completedBaseQuery
                .OrderBy(x => x.Achievement.GlobalUnlockRate <= 0 ? 9999 : x.Achievement.GlobalUnlockRate)
                .ThenByDescending(x => x.UnlockTime)
                .ThenBy(x => x.Achievement.Title)
                .Skip((currentPage - 1) * safePageSize)
                .Take(safePageSize))
                .ToListAsync();

            ViewBag.RecentAchievements = recentAchievements;
            ViewBag.RareAchievements = rareAchievements;
            ViewBag.PagedAchievements = pagedAchievements;
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = safePageSize;
            ViewBag.TotalAchievements = totalAchievements;
            ViewBag.GamesCount = stats?.GamesCount ?? 0;
            ViewBag.LegendaryCount = stats?.LegendaryCount ?? 0;
            ViewBag.EpicCount = stats?.EpicCount ?? 0;
            ViewBag.RareCount = stats?.RareCount ?? 0;
            ViewBag.CommonCount = stats?.CommonCount ?? 0;

            var rank = await _db.Users.AsNoTracking().CountAsync(u => u.TotalAchievements > user.TotalAchievements);
            ViewBag.Rank = rank + 1;
            ViewBag.IsOwner = isOwner;
            ViewBag.ProfileAvatarUrl = profileAvatarUrl;

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

            try
            {
                await SyncAchievementsForUserAsync(user.Id, true);
                TempData["ProfileSyncSuccess"] = "Синхронизация завершена.";
            }
            catch (Exception ex)
            {
                TempData["ProfileSyncError"] = $"Ошибка синхронизации: {ex.Message}";
            }

            return RedirectToAction(nameof(UserProfile), new { steamId = user.SteamId });
        }

        private async Task SyncAchievementsForUserAsync(int userId, bool force = false)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return;

            if (!force && user.LastSync.HasValue && (DateTime.UtcNow - user.LastSync.Value).TotalMinutes < 10)
                return;

            var localGames = await _db.Games
                .AsNoTracking()
                .Where(g => g.SteamAppId > 0 && g.Achievements.Any())
                .Select(g => new SyncGame
                {
                    SteamAppId = g.SteamAppId,
                    Achievements = g.Achievements
                        .Select(a => new SyncAchievement
                        {
                            Id = a.Id,
                            ApiName = a.ApiName
                        })
                        .ToList()
                })
                .ToListAsync();

            if (localGames.Count == 0)
            {
                user.TotalAchievements = 0;
                user.LastSync = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return;
            }

            var ownedGames = await _steamService.GetOwnedGames(user.SteamId);
            var ownedAppIds = ownedGames.Select(g => g.AppId).ToHashSet();
            if (ownedAppIds.Count > 0)
                localGames = localGames.Where(g => ownedAppIds.Contains(g.SteamAppId)).ToList();

            var allAchievementIds = localGames
                .SelectMany(g => g.Achievements)
                .Select(a => a.Id)
                .Distinct()
                .ToList();

            var existingUserAchievements = await _db.UserAchievements
                .Where(x => x.UserId == userId && allAchievementIds.Contains(x.AchievementId))
                .ToDictionaryAsync(x => x.AchievementId);

            var now = DateTime.UtcNow;
            using var semaphore = new SemaphoreSlim(SteamSyncConcurrency);

            var syncResults = await Task.WhenAll(localGames.Select(async game =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return new SyncGameResult
                    {
                        Game = game,
                        PlayerAchievements = await _steamService.GetPlayerAchievements(user.SteamId, game.SteamAppId)
                    };
                }
                catch
                {
                    return null;
                }
                finally
                {
                    semaphore.Release();
                }
            }));

            foreach (var result in syncResults)
            {
                if (result?.PlayerAchievements == null || result.PlayerAchievements.Count == 0)
                    continue;

                var completedMap = result.PlayerAchievements
                    .Where(x => !string.IsNullOrWhiteSpace(x.ApiName))
                    .GroupBy(x => Normalize(x.ApiName))
                    .ToDictionary(g => g.Key, g => g.Any(x => x.Achieved));

                foreach (var achievement in result.Game.Achievements)
                {
                    var normalizedApiName = Normalize(achievement.ApiName);
                    var completed = completedMap.TryGetValue(normalizedApiName, out var done) && done;

                    if (existingUserAchievements.TryGetValue(achievement.Id, out var existing))
                    {
                        existing.Completed = completed;

                        if (completed && existing.UnlockTime == null)
                            existing.UnlockTime = now;
                    }
                    else
                    {
                        _db.UserAchievements.Add(new UserAchievement
                        {
                            UserId = userId,
                            AchievementId = achievement.Id,
                            Completed = completed,
                            UnlockTime = completed ? now : null
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();

            user.TotalAchievements = await _db.UserAchievements
                .CountAsync(x => x.UserId == userId && x.Completed);

            user.LastSync = now;
            await _db.SaveChangesAsync();
        }

        private sealed class SyncGame
        {
            public int SteamAppId { get; set; }
            public List<SyncAchievement> Achievements { get; set; } = new();
        }

        private sealed class SyncAchievement
        {
            public int Id { get; set; }
            public string ApiName { get; set; } = "";
        }

        private sealed class SyncGameResult
        {
            public SyncGame Game { get; set; } = null!;
            public List<SteamPlayerAchievement> PlayerAchievements { get; set; } = new();
        }
    }
}
