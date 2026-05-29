using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
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
        private readonly QuestProgressService _questProgressService;
        private readonly NotificationService _notificationService;
        private readonly AchievementSyncService _achievementSyncService;

        public ProfileController(
            ApplicationDbContext db,
            SteamService steamService,
            UserManager<ApplicationUser> userManager,
            CacheService cacheService,
            QuestProgressService questProgressService,
            NotificationService notificationService,
            AchievementSyncService achievementSyncService)
        {
            _db = db;
            _steamService = steamService;
            _userManager = userManager;
            _cacheService = cacheService;
            _questProgressService = questProgressService;
            _notificationService = notificationService;
            _achievementSyncService = achievementSyncService;
        }

        private static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "";

            return s.Trim().ToLowerInvariant();
        }

        private static IQueryable<UserAchievement> ApplyRarityFilter(IQueryable<UserAchievement> query, string rarity)
        {
            return rarity switch
            {
                "legendary" => query.Where(x => x.Achievement.GlobalUnlockRate < 1),
                "epic" => query.Where(x => x.Achievement.GlobalUnlockRate >= 1 && x.Achievement.GlobalUnlockRate < 5),
                "rare" => query.Where(x => x.Achievement.GlobalUnlockRate >= 5 && x.Achievement.GlobalUnlockRate < 10),
                "common" => query.Where(x => x.Achievement.GlobalUnlockRate >= 10),
                _ => query
            };
        }

        private static IQueryable<AchievementCardViewModel> ToAchievementCards(IQueryable<UserAchievement> query)
        {
            return query.Select(x => new AchievementCardViewModel
            {
                Id = x.Id,
                Title = x.Achievement.Title,
                Description = x.Achievement.Description,
                GameName = x.Achievement.Game.Name,
                GameId = x.Achievement.GameId,
                GameAvatarUrl = x.Achievement.Game.AvatarUrl,
                IconUrl = !string.IsNullOrWhiteSpace(x.Achievement.IconUrl)
                    ? x.Achievement.IconUrl
                    : x.IconUrl,
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
        public async Task<IActionResult> UserProfile(string steamId, int page = 1, int pageSize = 24, string rarity = "all")
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return View("~/Views/Shared/NotFound.cshtml", "Профиль не найден.");

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.SteamId == steamId);
            if (user == null)
                return View("~/Views/Shared/NotFound.cshtml", "Профиль пользователя не найден.");

            var identityUser = await _userManager.GetUserAsync(User);
            var isOwner = identityUser?.SteamId == user.SteamId;

            if (!user.IsProfilePublic && !isOwner)
                return View("~/Views/Shared/NotFound.cshtml", "Профиль не найден или закрыт настройками приватности.");

            var profileAvatarUrl = !string.IsNullOrWhiteSpace(user.AvatarID)
                ? user.AvatarID
                : (!string.IsNullOrWhiteSpace(identityUser?.AvatarUrl) ? identityUser!.AvatarUrl : "/images/default_avatar.png");

            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 12, 60);

            var completedBaseQuery = _db.UserAchievements
                .AsNoTracking()
                .Where(x => x.UserId == user.Id && x.Completed);

            var achievementList = await BuildAchievementListAsync(user.Id, safePage, safePageSize, rarity);
            var currentPage = achievementList.CurrentPage;

            var nowUtc = DateTime.UtcNow;
            var timelineStart = new DateTime(nowUtc.Year - 9, 1, 1);
            var achievementUnlockTimes = await completedBaseQuery
                .Where(x => x.UnlockTime.HasValue && x.UnlockTime.Value >= timelineStart)
                .Select(x => x.UnlockTime!.Value)
                .ToListAsync();

            var profileStats = await completedBaseQuery
                .GroupBy(x => 1)
                .Select(g => new
                {
                    TotalCount = g.Count(),
                    GamesCount = g.Select(x => x.Achievement.GameId).Distinct().Count(),
                    LegendaryCount = g.Count(x => x.Achievement.GlobalUnlockRate < 1),
                    EpicCount = g.Count(x => x.Achievement.GlobalUnlockRate >= 1 && x.Achievement.GlobalUnlockRate < 5),
                    RareCount = g.Count(x => x.Achievement.GlobalUnlockRate >= 5 && x.Achievement.GlobalUnlockRate < 10),
                    CommonCount = g.Count(x => x.Achievement.GlobalUnlockRate >= 10)
                })
                .FirstOrDefaultAsync();

            var totalAchievements = profileStats?.TotalCount ?? 0;

            var achievementLevelInfo = AchievementLevelService.Calculate(
                profileStats?.LegendaryCount ?? 0,
                profileStats?.EpicCount ?? 0,
                profileStats?.RareCount ?? 0,
                profileStats?.CommonCount ?? 0);
            var levelInfo = AchievementLevelService.Calculate(achievementLevelInfo.TotalXp + user.QuestExperience);

            var recentAchievements = await ToAchievementCards(completedBaseQuery
                .OrderByDescending(x => x.UnlockTime ?? DateTime.MinValue)
                .ThenByDescending(x => x.Id)
                .Take(8))
                .ToListAsync();

            ViewBag.RecentAchievements = recentAchievements;
            ViewBag.RareAchievements = currentPage == 1
                ? achievementList.Items
                    .Where(x => x.GlobalUnlockRate > 0 && x.GlobalUnlockRate < 10)
                    .Take(6)
                    .ToList()
                : new List<AchievementCardViewModel>();
            ApplyAchievementListViewBag(achievementList, user.SteamId);
            ViewBag.TotalAchievements = totalAchievements;
            ViewBag.GamesCount = profileStats?.GamesCount ?? 0;
            ViewBag.LegendaryCount = profileStats?.LegendaryCount ?? 0;
            ViewBag.EpicCount = profileStats?.EpicCount ?? 0;
            ViewBag.RareCount = profileStats?.RareCount ?? 0;
            ViewBag.CommonCount = profileStats?.CommonCount ?? 0;
            ViewBag.AchievementTimeline = BuildAchievementTimeline(achievementUnlockTimes, nowUtc);
            ViewBag.LevelInfo = levelInfo;
            ViewBag.ProfileLevel = levelInfo.Level;
            ViewBag.ProfileXp = levelInfo.TotalXp;
            ViewBag.AchievementXp = achievementLevelInfo.TotalXp;
            ViewBag.QuestXp = user.QuestExperience;

            var rank = await _db.Users.AsNoTracking().CountAsync(u => u.TotalAchievements > user.TotalAchievements);
            ViewBag.Rank = rank + 1;
            ViewBag.IsOwner = isOwner;
            ViewBag.ProfileAvatarUrl = profileAvatarUrl;

            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> Achievements(string steamId, int page = 1, int pageSize = 24, string rarity = "all")
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return NotFound();

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.SteamId == steamId);
            if (user == null)
                return NotFound();

            var identityUser = await _userManager.GetUserAsync(User);
            var isOwner = identityUser?.SteamId == user.SteamId;

            if (!user.IsProfilePublic && !isOwner)
                return NotFound();

            var achievementList = await BuildAchievementListAsync(user.Id, page, pageSize, rarity);
            ApplyAchievementListViewBag(achievementList, user.SteamId);

            return PartialView("_ProfileAchievementsList");
        }

        private async Task<ProfileAchievementListResult> BuildAchievementListAsync(
            int userId,
            int page,
            int pageSize,
            string rarity)
        {
            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 12, 60);
            var selectedRarity = Normalize(rarity);

            if (selectedRarity is not ("legendary" or "epic" or "rare" or "common"))
                selectedRarity = "all";

            var completedBaseQuery = _db.UserAchievements
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Completed);

            var filteredBaseQuery = ApplyRarityFilter(completedBaseQuery, selectedRarity);
            var filteredAchievements = await filteredBaseQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(filteredAchievements / (double)safePageSize));
            var currentPage = Math.Min(safePage, totalPages);

            var pagedAchievements = await ToAchievementCards(filteredBaseQuery
                .OrderByDescending(x => x.UnlockTime ?? DateTime.MinValue)
                .ThenByDescending(x => x.Id)
                .Skip((currentPage - 1) * safePageSize)
                .Take(safePageSize))
                .ToListAsync();

            return new ProfileAchievementListResult
            {
                Items = pagedAchievements,
                CurrentPage = currentPage,
                TotalPages = totalPages,
                PageSize = safePageSize,
                FilteredAchievements = filteredAchievements,
                CurrentRarityFilter = selectedRarity
            };
        }

        private void ApplyAchievementListViewBag(ProfileAchievementListResult achievementList, string steamId)
        {
            ViewBag.PagedAchievements = achievementList.Items;
            ViewBag.CurrentPage = achievementList.CurrentPage;
            ViewBag.TotalPages = achievementList.TotalPages;
            ViewBag.PageSize = achievementList.PageSize;
            ViewBag.FilteredAchievements = achievementList.FilteredAchievements;
            ViewBag.CurrentRarityFilter = achievementList.CurrentRarityFilter;
            ViewBag.ProfileSteamId = steamId;
        }

        private sealed class ProfileAchievementListResult
        {
            public List<AchievementCardViewModel> Items { get; set; } = new();

            public int CurrentPage { get; set; }

            public int TotalPages { get; set; }

            public int PageSize { get; set; }

            public int FilteredAchievements { get; set; }

            public string CurrentRarityFilter { get; set; } = "all";
        }

        private static AchievementTimelineViewModel BuildAchievementTimeline(
            IReadOnlyList<DateTime> unlockTimes,
            DateTime nowUtc)
        {
            var culture = CultureInfo.GetCultureInfo("ru-RU");
            var yearStart = new DateTime(nowUtc.Year - 9, 1, 1);
            var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1).AddMonths(-11);
            var dayStart = nowUtc.Date.AddDays(-29);

            var yearCounts = unlockTimes
                .Where(x => x >= yearStart)
                .GroupBy(x => x.Year)
                .ToDictionary(g => g.Key, g => g.Count());

            var monthCounts = unlockTimes
                .Where(x => x >= monthStart)
                .GroupBy(x => new { x.Year, x.Month })
                .ToDictionary(g => (g.Key.Year, g.Key.Month), g => g.Count());

            var dayCounts = unlockTimes
                .Where(x => x >= dayStart)
                .GroupBy(x => x.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var yearPoints = Enumerable.Range(0, 10)
                .Select(index =>
                {
                    var year = yearStart.Year + index;
                    var label = year.ToString(CultureInfo.InvariantCulture);
                    return (Label: label, ShortLabel: label, Count: yearCounts.GetValueOrDefault(year));
                })
                .ToList();

            var monthPoints = Enumerable.Range(0, 12)
                .Select(index =>
                {
                    var month = monthStart.AddMonths(index);
                    return (
                        Label: month.ToString("MMMM yyyy", culture),
                        ShortLabel: month.ToString("MMM", culture),
                        Count: monthCounts.GetValueOrDefault((month.Year, month.Month)));
                })
                .ToList();

            var dayPoints = Enumerable.Range(0, 30)
                .Select(index =>
                {
                    var day = dayStart.AddDays(index);
                    return (
                        Label: day.ToString("dd MMMM", culture),
                        ShortLabel: day.ToString("dd.MM", CultureInfo.InvariantCulture),
                        Count: dayCounts.GetValueOrDefault(day));
                })
                .ToList();

            return new AchievementTimelineViewModel
            {
                Series = new List<AchievementTimelineSeriesViewModel>
                {
                    BuildTimelineSeries("decade", "10 лет", "По годам", yearPoints),
                    BuildTimelineSeries("year", "1 год", "По месяцам", monthPoints),
                    BuildTimelineSeries("month", "Месяц", "Последние 30 дней", dayPoints)
                }
            };
        }

        private static AchievementTimelineSeriesViewModel BuildTimelineSeries(
            string key,
            string label,
            string description,
            IReadOnlyList<(string Label, string ShortLabel, int Count)> points)
        {
            var maxCount = points.Count == 0 ? 0 : points.Max(x => x.Count);
            var scale = Math.Max(1, maxCount);

            return new AchievementTimelineSeriesViewModel
            {
                Key = key,
                Label = label,
                Description = description,
                Total = points.Sum(x => x.Count),
                MaxCount = maxCount,
                Points = points
                    .Select(x => new AchievementTimelinePointViewModel
                    {
                        Label = x.Label,
                        ShortLabel = x.ShortLabel,
                        Count = x.Count,
                        Percent = x.Count == 0 ? 0 : Math.Round(x.Count * 100.0 / scale, 2)
                    })
                    .ToList()
            };
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
                await SyncProfileProgressAsync(user.Id, force: true);
                TempData["ProfileSyncSuccess"] = "Синхронизация завершена.";
            }
            catch (Exception ex)
            {
                TempData["ProfileSyncError"] = $"Ошибка синхронизации: {ex.Message}";
            }

            return RedirectToAction(nameof(UserProfile), new { steamId = user.SteamId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoSync()
        {
            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
                return Unauthorized();

            var user = await _db.Users.FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);
            if (user == null)
                return NotFound();

            try
            {
                var result = await SyncProfileProgressAsync(user.Id, force: true);

                return Json(new
                {
                    skipped = result.Skipped,
                    changed = result.HasChanges,
                    totalAchievements = result.TotalAchievements,
                    syncedAt = result.SyncedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        private async Task<AchievementSyncResult> SyncProfileProgressAsync(
            int userId,
            bool force,
            TimeSpan? minInterval = null)
        {
            var result = await _achievementSyncService.SyncAchievementsForUserAsync(userId, force, minInterval);

            if (!result.Skipped)
            {
                await _questProgressService.EvaluateDailyQuestAsync(userId);
                await _questProgressService.EvaluateAutomaticChallengesForUserAsync(userId);
                await _notificationService.EvaluateProfileMilestonesAsync(userId);
                await _db.SaveChangesAsync();
            }

            return result;
        }

    }
}
