using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
            var identityUser = User.Identity?.IsAuthenticated == true
                ? await _userManager.GetUserAsync(User)
                : null;
            var currentSteamId = identityUser?.SteamId;
            var weekStart = DateTime.UtcNow.AddDays(-7);

            var userStats = await _context.Users
                .AsNoTracking()
                .Where(u => !string.IsNullOrEmpty(u.SteamId))
                .Select(u => new LeaderboardUserStats
                {
                    UserId = u.Id,
                    SteamId = u.SteamId,
                    SteamName = u.SteamName,
                    AvatarUrl = u.AvatarID,
                    TotalAchievements = u.TotalAchievements,
                    WeeklyAchievements = u.UserAchievements.Count(x => x.Completed && x.UnlockTime >= weekStart),
                    QuestExperience = u.QuestExperience,
                    LegendaryCount = u.UserAchievements.Count(x => x.Completed && x.Achievement.GlobalUnlockRate < 1),
                    EpicCount = u.UserAchievements.Count(x => x.Completed && x.Achievement.GlobalUnlockRate >= 1 && x.Achievement.GlobalUnlockRate < 5),
                    RareCount = u.UserAchievements.Count(x => x.Completed && x.Achievement.GlobalUnlockRate >= 5 && x.Achievement.GlobalUnlockRate < 10),
                    CommonCount = u.UserAchievements.Count(x => x.Completed && x.Achievement.GlobalUnlockRate >= 10)
                })
                .ToListAsync();

            var entries = userStats
                .Select(x => ToLeaderboardEntry(x, currentSteamId))
                .ToList();

            var achievementLeaders = entries
                .OrderByDescending(x => x.TotalAchievements)
                .ThenBy(x => x.SteamName)
                .ThenBy(x => x.UserId)
                .Select((entry, index) => entry.WithRank(index + 1))
                .ToList();

            var levelLeaders = entries
                .OrderByDescending(x => x.Level)
                .ThenByDescending(x => x.TotalXp)
                .ThenByDescending(x => x.TotalAchievements)
                .ThenBy(x => x.SteamName)
                .ThenBy(x => x.UserId)
                .Select((entry, index) => entry.WithRank(index + 1))
                .ToList();

            var weeklyAchievementLeaders = entries
                .OrderByDescending(x => x.WeeklyAchievements)
                .ThenByDescending(x => x.TotalAchievements)
                .ThenBy(x => x.SteamName)
                .ThenBy(x => x.UserId)
                .Select((entry, index) => entry.WithRank(index + 1))
                .ToList();

            var model = new LeaderboardViewModel
            {
                AchievementLeaders = achievementLeaders,
                LevelLeaders = levelLeaders,
                WeeklyAchievementLeaders = weeklyAchievementLeaders,
                CurrentAchievementEntry = achievementLeaders.FirstOrDefault(x => x.IsCurrentUser),
                CurrentLevelEntry = levelLeaders.FirstOrDefault(x => x.IsCurrentUser),
                CurrentWeeklyAchievementEntry = weeklyAchievementLeaders.FirstOrDefault(x => x.IsCurrentUser),
                IsSignedIn = identityUser != null
            };

            return View(model);
        }

        public IActionResult Challenges()
        {
            return RedirectToAction("Index", "Challenges");
        }

        private static LeaderboardEntryViewModel ToLeaderboardEntry(
            LeaderboardUserStats stats,
            string? currentSteamId)
        {
            var achievementLevelInfo = AchievementLevelService.Calculate(
                stats.LegendaryCount,
                stats.EpicCount,
                stats.RareCount,
                stats.CommonCount);
            var levelInfo = AchievementLevelService.Calculate(achievementLevelInfo.TotalXp + stats.QuestExperience);

            return new LeaderboardEntryViewModel
            {
                UserId = stats.UserId,
                SteamId = stats.SteamId,
                SteamName = string.IsNullOrWhiteSpace(stats.SteamName) ? "Player" : stats.SteamName,
                AvatarUrl = string.IsNullOrWhiteSpace(stats.AvatarUrl) ? "/images/default_avatar.png" : stats.AvatarUrl,
                TotalAchievements = stats.TotalAchievements,
                WeeklyAchievements = stats.WeeklyAchievements,
                Level = levelInfo.Level,
                TotalXp = levelInfo.TotalXp,
                CurrentLevelXp = levelInfo.CurrentLevelXp,
                RequiredXp = levelInfo.RequiredXp,
                ProgressPercent = levelInfo.ProgressPercent,
                IsCurrentUser = !string.IsNullOrWhiteSpace(currentSteamId) && stats.SteamId == currentSteamId
            };
        }

        private sealed class LeaderboardUserStats
        {
            public int UserId { get; set; }

            public string SteamId { get; set; } = "";

            public string SteamName { get; set; } = "";

            public string AvatarUrl { get; set; } = "";

            public int TotalAchievements { get; set; }

            public int WeeklyAchievements { get; set; }

            public int QuestExperience { get; set; }

            public int LegendaryCount { get; set; }

            public int EpicCount { get; set; }

            public int RareCount { get; set; }

            public int CommonCount { get; set; }
        }
    }
}
