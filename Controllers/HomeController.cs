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
        private readonly CustomAchievementVotingService _customAchievementVotingService;

        public HomeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            CustomAchievementVotingService customAchievementVotingService)
        {
            _context = context;
            _userManager = userManager;
            _customAchievementVotingService = customAchievementVotingService;
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

        public async Task<IActionResult> AchievementVoting()
        {
            await _customAchievementVotingService.ResolveDueVotingAsync();

            var identityUser = User.Identity?.IsAuthenticated == true
                ? await _userManager.GetUserAsync(User)
                : null;
            var currentUser = string.IsNullOrWhiteSpace(identityUser?.SteamId)
                ? null
                : await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);

            var requests = await _context.CustomAchievementRequests
                .AsNoTracking()
                .Include(x => x.Game)
                .Include(x => x.RequestedByUser)
                .Include(x => x.Votes)
                .Where(x => x.Status == CustomAchievementRequestStatuses.Voting)
                .OrderBy(x => x.VotingEndsAt)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();

            var model = new AchievementVotingViewModel
            {
                IsSignedIn = identityUser != null,
                Items = requests.Select(x =>
                {
                    var userVote = currentUser == null
                        ? null
                        : x.Votes.FirstOrDefault(v => v.UserId == currentUser.Id);

                    return new AchievementVotingItemViewModel
                    {
                        Id = x.Id,
                        GameId = x.GameId,
                        GameName = x.Game.Name,
                        GameAvatarUrl = x.Game.AvatarUrl,
                        Title = x.Title,
                        Description = x.Description,
                        ObtainMethod = x.ObtainMethod,
                        IconUrl = string.IsNullOrWhiteSpace(x.IconUrl) ? x.Game.AvatarUrl : x.IconUrl,
                        RequestedBy = x.RequestedByUser != null ? x.RequestedByUser.SteamName : "Неизвестно",
                        CreatedAt = x.CreatedAt,
                        VotingStartedAt = x.VotingStartedAt,
                        VotingEndsAt = x.VotingEndsAt,
                        PositiveVotes = x.Votes.Count(v => v.IsPositive),
                        NegativeVotes = x.Votes.Count(v => !v.IsPositive),
                        CurrentUserVote = userVote?.IsPositive,
                        CanVote = currentUser != null && x.RequestedByUserId != currentUser.Id
                    };
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoteCustomAchievement(int id, bool approve)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
                return RedirectToAction("Login", "Account");

            var publicUser = await _context.Users.FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);
            if (publicUser == null)
                return RedirectToAction("Login", "Account");

            var request = await _context.CustomAchievementRequests
                .Include(x => x.Game)
                .Include(x => x.Votes)
                .FirstOrDefaultAsync(x => x.Id == id && x.Status == CustomAchievementRequestStatuses.Voting);

            if (request == null)
            {
                TempData["VotingError"] = "Голосование уже завершено или заявка не найдена.";
                return RedirectToAction(nameof(AchievementVoting));
            }

            if (request.VotingEndsAt <= DateTime.UtcNow)
            {
                await _customAchievementVotingService.TryResolveAsync(request);
                await _context.SaveChangesAsync();
                TempData["VotingError"] = "Голосование уже завершилось.";
                return RedirectToAction(nameof(AchievementVoting));
            }

            if (request.RequestedByUserId == publicUser.Id)
            {
                TempData["VotingError"] = "Нельзя голосовать за свою заявку.";
                return RedirectToAction(nameof(AchievementVoting));
            }

            var vote = request.Votes.FirstOrDefault(x => x.UserId == publicUser.Id);
            if (vote == null)
            {
                request.Votes.Add(new CustomAchievementVote
                {
                    UserId = publicUser.Id,
                    IsPositive = approve,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                vote.IsPositive = approve;
                vote.CreatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            request = await _context.CustomAchievementRequests
                .Include(x => x.Game)
                .Include(x => x.Votes)
                .FirstAsync(x => x.Id == id);
            await _customAchievementVotingService.TryResolveAsync(request);
            await _context.SaveChangesAsync();

            TempData["VotingSuccess"] = approve ? "Голос за добавление учтен." : "Голос против добавления учтен.";
            return RedirectToAction(nameof(AchievementVoting));
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
                AvatarUrl = string.IsNullOrWhiteSpace(stats.AvatarUrl) ? "/img/standart.jpg" : stats.AvatarUrl,
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
