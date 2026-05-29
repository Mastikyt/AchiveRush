using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _db;

        public NotificationService(ApplicationDbContext db)
        {
            _db = db;
        }

        public Task AddAsync(int? userId, string type, string title, string message, string url = "")
        {
            if (!userId.HasValue)
                return Task.CompletedTask;

            _db.Notifications.Add(new Notification
            {
                UserId = userId.Value,
                Type = type,
                Title = title,
                Message = message,
                Url = url,
                CreatedAt = DateTime.UtcNow
            });

            return Task.CompletedTask;
        }

        public async Task EvaluateProfileMilestonesAsync(int userId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return;

            var currentLevel = await CalculateCurrentLevelAsync(user);
            if (user.LastNotifiedLevel <= 0)
            {
                user.LastNotifiedLevel = currentLevel;
            }
            else if (currentLevel > user.LastNotifiedLevel)
            {
                await AddAsync(
                    user.Id,
                    NotificationTypes.Level,
                    "Новый уровень",
                    $"Ты поднялся до {currentLevel} уровня.",
                    $"/Profile/UserProfile?steamId={Uri.EscapeDataString(user.SteamId)}");
                user.LastNotifiedLevel = currentLevel;
            }

            var currentRank = await GetLeaderboardRankAsync(user.Id);
            if (currentRank <= 0)
                return;

            if (!user.LastKnownLeaderboardRank.HasValue || user.LastKnownLeaderboardRank.Value <= 0)
            {
                user.LastKnownLeaderboardRank = currentRank;
            }
            else if (currentRank < user.LastKnownLeaderboardRank.Value)
            {
                await AddAsync(
                    user.Id,
                    NotificationTypes.Leaderboard,
                    "Позиция в лидерборде выросла",
                    $"Ты поднялся с #{user.LastKnownLeaderboardRank.Value} на #{currentRank}.",
                    "/Home/Leaderboard");
                user.LastKnownLeaderboardRank = currentRank;
            }
            else if (currentRank > user.LastKnownLeaderboardRank.Value)
            {
                user.LastKnownLeaderboardRank = currentRank;
            }
        }

        private async Task<int> CalculateCurrentLevelAsync(User user)
        {
            var stats = await _db.UserAchievements
                .AsNoTracking()
                .Where(x => x.UserId == user.Id && x.Completed)
                .GroupBy(x => 1)
                .Select(g => new
                {
                    LegendaryCount = g.Count(x => x.Achievement.GlobalUnlockRate < 1),
                    EpicCount = g.Count(x => x.Achievement.GlobalUnlockRate >= 1 && x.Achievement.GlobalUnlockRate < 5),
                    RareCount = g.Count(x => x.Achievement.GlobalUnlockRate >= 5 && x.Achievement.GlobalUnlockRate < 10),
                    CommonCount = g.Count(x => x.Achievement.GlobalUnlockRate >= 10)
                })
                .FirstOrDefaultAsync();

            var achievementXp = AchievementLevelService.Calculate(
                stats?.LegendaryCount ?? 0,
                stats?.EpicCount ?? 0,
                stats?.RareCount ?? 0,
                stats?.CommonCount ?? 0).TotalXp;

            return AchievementLevelService.Calculate(achievementXp + user.QuestExperience).Level;
        }

        private async Task<int> GetLeaderboardRankAsync(int userId)
        {
            var orderedUserIds = await _db.Users
                .AsNoTracking()
                .Where(u => !string.IsNullOrEmpty(u.SteamId))
                .OrderByDescending(u => u.TotalAchievements)
                .ThenBy(u => u.SteamName)
                .Select(u => u.Id)
                .ToListAsync();

            var index = orderedUserIds.IndexOf(userId);
            return index < 0 ? 0 : index + 1;
        }
    }
}
