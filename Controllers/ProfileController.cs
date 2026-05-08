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

            var selectedRarity = Normalize(rarity);
            if (selectedRarity is not ("legendary" or "epic" or "rare" or "common"))
                selectedRarity = "all";

            var filteredBaseQuery = ApplyRarityFilter(completedBaseQuery, selectedRarity);

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
            var filteredAchievements = selectedRarity == "all"
                ? totalAchievements
                : await filteredBaseQuery.CountAsync();

            var achievementLevelInfo = AchievementLevelService.Calculate(
                profileStats?.LegendaryCount ?? 0,
                profileStats?.EpicCount ?? 0,
                profileStats?.RareCount ?? 0,
                profileStats?.CommonCount ?? 0);
            var levelInfo = AchievementLevelService.Calculate(achievementLevelInfo.TotalXp + user.QuestExperience);

            var totalPages = Math.Max(1, (int)Math.Ceiling(filteredAchievements / (double)safePageSize));
            var currentPage = Math.Min(safePage, totalPages);

            var recentAchievements = await ToAchievementCards(completedBaseQuery
                .OrderByDescending(x => x.UnlockTime ?? DateTime.MinValue)
                .ThenByDescending(x => x.Id)
                .Take(8))
                .ToListAsync();

            var pagedAchievements = await ToAchievementCards(filteredBaseQuery
                .OrderByDescending(x => x.UnlockTime ?? DateTime.MinValue)
                .ThenByDescending(x => x.Id)
                .Skip((currentPage - 1) * safePageSize)
                .Take(safePageSize))
                .ToListAsync();

            ViewBag.RecentAchievements = recentAchievements;
            ViewBag.RareAchievements = currentPage == 1
                ? pagedAchievements
                    .Where(x => x.GlobalUnlockRate > 0 && x.GlobalUnlockRate < 10)
                    .Take(6)
                    .ToList()
                : new List<AchievementCardViewModel>();
            ViewBag.PagedAchievements = pagedAchievements;
            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = safePageSize;
            ViewBag.TotalAchievements = totalAchievements;
            ViewBag.FilteredAchievements = filteredAchievements;
            ViewBag.CurrentRarityFilter = selectedRarity;
            ViewBag.GamesCount = profileStats?.GamesCount ?? 0;
            ViewBag.LegendaryCount = profileStats?.LegendaryCount ?? 0;
            ViewBag.EpicCount = profileStats?.EpicCount ?? 0;
            ViewBag.RareCount = profileStats?.RareCount ?? 0;
            ViewBag.CommonCount = profileStats?.CommonCount ?? 0;
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
                await _achievementSyncService.SyncAchievementsForUserAsync(user.Id, true);
                await _questProgressService.EvaluateDailyQuestAsync(user.Id);
                await _questProgressService.EvaluateAutomaticChallengesForUserAsync(user.Id);
                await _notificationService.EvaluateProfileMilestonesAsync(user.Id);
                await _db.SaveChangesAsync();
                TempData["ProfileSyncSuccess"] = "Синхронизация завершена.";
            }
            catch (Exception ex)
            {
                TempData["ProfileSyncError"] = $"Ошибка синхронизации: {ex.Message}";
            }

            return RedirectToAction(nameof(UserProfile), new { steamId = user.SteamId });
        }

    }
}
