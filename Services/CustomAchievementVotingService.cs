using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class CustomAchievementVotingService
    {
        private static readonly TimeSpan EarlyApprovalDelay = TimeSpan.FromDays(1);
        private static readonly TimeSpan VotingDuration = TimeSpan.FromDays(3);

        private readonly ApplicationDbContext _db;
        private readonly NotificationService _notificationService;

        public CustomAchievementVotingService(
            ApplicationDbContext db,
            NotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        public static DateTime GetVotingEnd(DateTime startedAtUtc)
        {
            return startedAtUtc.Add(VotingDuration);
        }

        public async Task<int> ResolveDueVotingAsync()
        {
            var now = DateTime.UtcNow;
            var requests = await _db.CustomAchievementRequests
                .Include(x => x.Game)
                .Include(x => x.Votes)
                .Where(x => x.Status == CustomAchievementRequestStatuses.Voting)
                .Where(x => x.VotingStartedAt != null && x.VotingEndsAt != null)
                .Where(x => x.VotingEndsAt <= now || x.VotingStartedAt <= now.Subtract(EarlyApprovalDelay))
                .ToListAsync();

            var resolved = 0;
            foreach (var request in requests)
            {
                if (await TryResolveAsync(request, now))
                    resolved++;
            }

            if (resolved > 0)
                await _db.SaveChangesAsync();

            return resolved;
        }

        public async Task<bool> TryResolveAsync(CustomAchievementRequest request, DateTime? nowUtc = null)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            if (request.Status != CustomAchievementRequestStatuses.Voting ||
                request.VotingStartedAt == null ||
                request.VotingEndsAt == null)
            {
                return false;
            }

            var positiveVotes = request.Votes.Count(x => x.IsPositive);
            var negativeVotes = request.Votes.Count(x => !x.IsPositive);
            var totalVotes = positiveVotes + negativeVotes;
            var canApproveEarly = totalVotes > 0 &&
                                  request.VotingStartedAt.Value.Add(EarlyApprovalDelay) <= now &&
                                  positiveVotes / (double)totalVotes > 0.8;

            if (canApproveEarly)
            {
                await ApproveRequestAsync(request, now);
                return true;
            }

            if (request.VotingEndsAt.Value > now)
                return false;

            if (positiveVotes > negativeVotes)
                await ApproveRequestAsync(request, now);
            else
                await RejectRequestAsync(request, now);

            return true;
        }

        private async Task ApproveRequestAsync(CustomAchievementRequest request, DateTime now)
        {
            var duplicate = await _db.Achievements.AnyAsync(x =>
                x.GameId == request.GameId &&
                x.Title == request.Title);

            if (duplicate)
            {
                request.Status = CustomAchievementRequestStatuses.Rejected;
                request.ResolvedAt = now;
                await _notificationService.AddAsync(
                    request.RequestedByUserId,
                    NotificationTypes.Achievement,
                    "Достижение отклонено",
                    $"Голосование по достижению «{request.Title}» завершилось, но похожее достижение уже есть в игре «{request.Game.Name}».",
                    $"/Games/Details/{request.GameId}");
                return;
            }

            _db.Achievements.Add(new Achievement
            {
                GameId = request.GameId,
                Title = request.Title,
                Description = request.Description,
                ObtainMethod = request.ObtainMethod,
                IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? request.Game.AvatarUrl : request.IconUrl,
                ApiName = $"custom:{request.GameId}:{Guid.NewGuid():N}",
                IsCustom = true,
                CreatedByUserId = request.RequestedByUserId,
                CreatedAt = now,
                GlobalUnlockRate = 0
            });

            request.Status = CustomAchievementRequestStatuses.Approved;
            request.ResolvedAt = now;
            await _notificationService.AddAsync(
                request.RequestedByUserId,
                NotificationTypes.Achievement,
                "Достижение добавлено",
                $"Пользователи одобрили достижение «{request.Title}» для игры «{request.Game.Name}».",
                $"/Games/Details/{request.GameId}");
        }

        private async Task RejectRequestAsync(CustomAchievementRequest request, DateTime now)
        {
            request.Status = CustomAchievementRequestStatuses.Rejected;
            request.ResolvedAt = now;
            await _notificationService.AddAsync(
                request.RequestedByUserId,
                NotificationTypes.Achievement,
                "Достижение отклонено",
                $"Голосование по достижению «{request.Title}» завершилось без большинства голосов за.",
                $"/Home/AchievementVoting");
        }
    }

    public class CustomAchievementVotingHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public CustomAchievementVotingHostedService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var voting = scope.ServiceProvider.GetRequiredService<CustomAchievementVotingService>();
                    await voting.ResolveDueVotingAsync();
                }
                catch
                {
                    // The next tick retries; voting must not stop the whole host.
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
