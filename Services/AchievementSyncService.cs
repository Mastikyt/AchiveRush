using Microsoft.EntityFrameworkCore;
using WebApplication1.DTO;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class AchievementSyncService
    {
        private const int SteamSyncConcurrency = 6;

        private readonly ApplicationDbContext _db;
        private readonly SteamService _steamService;

        public AchievementSyncService(ApplicationDbContext db, SteamService steamService)
        {
            _db = db;
            _steamService = steamService;
        }

        public async Task SyncAchievementsForUserAsync(int userId, bool force = false)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return;

            if (string.IsNullOrWhiteSpace(user.SteamId))
                return;

            if (!force && user.LastSync.HasValue && (DateTime.UtcNow - user.LastSync.Value).TotalMinutes < 10)
                return;

            var localGames = await _db.Games
                .AsNoTracking()
                .Where(g => g.SteamAppId > 0 && g.Achievements.Any(a => !a.IsCustom))
                .Select(g => new SyncGame
                {
                    SteamAppId = g.SteamAppId,
                    Achievements = g.Achievements
                        .Where(a => !a.IsCustom)
                        .Select(a => new SyncAchievement
                        {
                            Id = a.Id,
                            ApiName = a.ApiName,
                            IconUrl = a.IconUrl
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
            var playedOwnedAppIds = ownedGames
                .Where(g => g.PlaytimeForever > 0)
                .Select(g => g.AppId)
                .ToHashSet();

            localGames = localGames
                .OrderByDescending(g => playedOwnedAppIds.Contains(g.SteamAppId))
                .ToList();

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
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault(x => x.Achieved) ?? g.First());

                foreach (var achievement in result.Game.Achievements)
                {
                    var normalizedApiName = Normalize(achievement.ApiName);
                    var completed = completedMap.TryGetValue(normalizedApiName, out var steamAchievement) && steamAchievement.Achieved;
                    var unlockTime = steamAchievement?.UnlockTime ?? now;

                    if (existingUserAchievements.TryGetValue(achievement.Id, out var existing))
                    {
                        existing.Completed = completed;
                        if (!string.IsNullOrWhiteSpace(achievement.IconUrl))
                            existing.IconUrl = achievement.IconUrl;

                        if (completed)
                            existing.UnlockTime = unlockTime;
                    }
                    else
                    {
                        _db.UserAchievements.Add(new UserAchievement
                        {
                            UserId = userId,
                            AchievementId = achievement.Id,
                            Completed = completed,
                            UnlockTime = completed ? unlockTime : null,
                            IconUrl = achievement.IconUrl
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

        private static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "";

            return s.Trim().ToLowerInvariant();
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
            public string? IconUrl { get; set; }
        }

        private sealed class SyncGameResult
        {
            public SyncGame Game { get; set; } = null!;
            public List<SteamPlayerAchievement> PlayerAchievements { get; set; } = new();
        }
    }
}
