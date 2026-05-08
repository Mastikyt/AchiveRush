using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class QuestProgressService
    {
        private readonly ApplicationDbContext _db;
        private readonly NotificationService _notificationService;

        public QuestProgressService(ApplicationDbContext db, NotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        public async Task EnsureDailyQuestPoolAsync()
        {
            if (await _db.DailyQuests.AnyAsync())
                return;

            _db.DailyQuests.AddRange(
                new DailyQuest
                {
                    Title = "Разогрев",
                    Description = "Получи 1 достижение в любой игре из каталога.",
                    Difficulty = ChallengeDifficulties.Easy,
                    QuestType = DailyQuestTypes.EarnAchievements,
                    TargetValue = 1,
                    RewardExperience = 25
                },
                new DailyQuest
                {
                    Title = "Хороший темп",
                    Description = "Получи 3 достижения в любой игре из каталога.",
                    Difficulty = ChallengeDifficulties.Normal,
                    QuestType = DailyQuestTypes.EarnAchievements,
                    TargetValue = 3,
                    RewardExperience = 60
                },
                new DailyQuest
                {
                    Title = "Редкая добыча",
                    Description = "Получи 1 редкое достижение с глобальным процентом ниже 10%.",
                    Difficulty = ChallengeDifficulties.Hard,
                    QuestType = DailyQuestTypes.EarnRareAchievements,
                    TargetValue = 1,
                    RewardExperience = 100
                },
                new DailyQuest
                {
                    Title = "Идеальное закрытие",
                    Description = "Закрой на 100% одну игру из каталога.",
                    Difficulty = ChallengeDifficulties.Legendary,
                    QuestType = DailyQuestTypes.CompleteGame100,
                    TargetValue = 1,
                    RewardExperience = 180
                });

            await _db.SaveChangesAsync();
        }

        public async Task EnsureStarterChallengesAsync()
        {
            var games = await _db.Games
                .AsNoTracking()
                .Where(g => g.Achievements.Any(a => !a.IsCustom))
                .OrderBy(g => g.Name)
                .Take(8)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.AvatarUrl,
                    AchievementsCount = g.Achievements.Count(a => !a.IsCustom),
                    RareAchievementsCount = g.Achievements.Count(a => !a.IsCustom && a.GlobalUnlockRate > 0 && a.GlobalUnlockRate < 10)
                })
                .ToListAsync();

            if (games.Count == 0)
                return;

            foreach (var game in games)
            {
                var targetAchievements = Math.Clamp(Math.Min(game.AchievementsCount, 5), 1, 20);
                var templates = new List<Challenge>
                {
                    new()
                    {
                        Title = $"100% достижений: {game.Name}",
                        Description = $"Получи все {game.AchievementsCount} Steam-достижений в игре. Система подтвердит выполнение после синхронизации профиля.",
                        Difficulty = ChallengeDifficulties.Hard,
                        Category = ChallengeCategories.Completion,
                        ChallengeType = ChallengeTypes.Completion,
                        VerificationType = ChallengeVerificationTypes.Automatic,
                        AutoGoalType = ChallengeAutoGoalTypes.GameCompletion100,
                        TargetValue = 1,
                        RewardExperience = 220,
                        ParticipantLimit = 50,
                        GameId = game.Id,
                        CoverImageUrl = game.AvatarUrl
                    },
                    new()
                    {
                        Title = $"Охота за ачивками: {game.Name}",
                        Description = $"Получи {targetAchievements} достижений в игре. Подойдет тем, кто хочет короткий и понятный забег.",
                        Difficulty = ChallengeDifficulties.Normal,
                        Category = ChallengeCategories.Collection,
                        ChallengeType = ChallengeTypes.AchievementHunt,
                        VerificationType = ChallengeVerificationTypes.Automatic,
                        AutoGoalType = ChallengeAutoGoalTypes.EarnAchievementsInGame,
                        TargetValue = targetAchievements,
                        RewardExperience = 90,
                        ParticipantLimit = 80,
                        GameId = game.Id,
                        CoverImageUrl = game.AvatarUrl
                    }
                };

                if (game.RareAchievementsCount > 0)
                {
                    templates.Add(new Challenge
                    {
                        Title = $"Редкая добыча: {game.Name}",
                        Description = "Получи редкое достижение с глобальным процентом ниже 10%.",
                        Difficulty = ChallengeDifficulties.Legendary,
                        Category = ChallengeCategories.RareHunt,
                        ChallengeType = ChallengeTypes.RareAchievementHunt,
                        VerificationType = ChallengeVerificationTypes.Automatic,
                        AutoGoalType = ChallengeAutoGoalTypes.EarnRareAchievementsInGame,
                        TargetValue = 1,
                        RewardExperience = 160,
                        ParticipantLimit = 40,
                        GameId = game.Id,
                        CoverImageUrl = game.AvatarUrl
                    });
                }

                foreach (var template in templates)
                {
                    var exists = await _db.Challenges.AnyAsync(c =>
                        c.GameId == game.Id &&
                        c.ChallengeType == template.ChallengeType &&
                        c.AutoGoalType == template.AutoGoalType &&
                        c.TargetValue == template.TargetValue);

                    if (exists)
                        continue;

                    template.Status = ChallengeStatuses.Approved;
                    template.CreatedAt = DateTime.UtcNow;
                    template.ReviewedAt = DateTime.UtcNow;
                    _db.Challenges.Add(template);
                }
            }

            await _db.SaveChangesAsync();
        }

        public async Task<DailyQuestAssignment?> EnsureDailyQuestForUserAsync(int userId)
        {
            var today = DateTime.UtcNow.Date;
            var assignment = await _db.DailyQuestAssignments
                .Include(a => a.DailyQuest)
                .FirstOrDefaultAsync(a => a.UserId == userId && a.AssignedDate == today);

            if (assignment != null)
                return assignment;

            var quests = await _db.DailyQuests
                .Where(q => q.IsActive)
                .OrderBy(q => q.Id)
                .ToListAsync();

            if (quests.Count == 0)
                return null;

            var quest = quests[Math.Abs(userId + today.DayOfYear) % quests.Count];
            assignment = new DailyQuestAssignment
            {
                UserId = userId,
                DailyQuestId = quest.Id,
                DailyQuest = quest,
                AssignedDate = today,
                CreatedAt = DateTime.UtcNow
            };

            _db.DailyQuestAssignments.Add(assignment);
            await EnsureDailyQuestStatAsync(userId, today, resetSnapshot: true);
            await _db.SaveChangesAsync();

            return assignment;
        }

        public async Task<string> RerollDailyQuestAsync(int userId)
        {
            await EnsureDailyQuestPoolAsync();
            var assignment = await EnsureDailyQuestForUserAsync(userId);
            if (assignment == null)
                return "Нет активных ежедневных квестов.";

            if (assignment.Completed)
                return "Выполненную ежедневку уже нельзя сменить.";

            if (assignment.RerolledAt.HasValue)
                return "Сегодня ежедневку уже меняли.";

            var quests = await _db.DailyQuests
                .Where(q => q.IsActive && q.Id != assignment.DailyQuestId)
                .OrderBy(q => q.Id)
                .ToListAsync();

            if (quests.Count == 0)
                return "Нет другого ежедневного квеста для замены.";

            var today = DateTime.UtcNow.Date;
            var quest = quests[Math.Abs(userId + today.DayOfYear + assignment.Id) % quests.Count];
            assignment.DailyQuestId = quest.Id;
            assignment.DailyQuest = quest;
            assignment.RerolledAt = DateTime.UtcNow;
            assignment.ProgressValue = 0;
            assignment.Completed = false;
            assignment.CompletedAt = null;
            assignment.RewardGranted = false;
            assignment.CreatedAt = DateTime.UtcNow;

            await EnsureDailyQuestStatAsync(userId, today, resetSnapshot: true);
            await _db.SaveChangesAsync();

            return "Ежедневный квест заменен.";
        }

        public async Task<DailyQuestAssignment?> EvaluateDailyQuestAsync(int userId)
        {
            await EnsureDailyQuestPoolAsync();
            var assignment = await EnsureDailyQuestForUserAsync(userId);
            if (assignment == null)
                return null;

            var progress = await GetDailyQuestProgressAsync(userId, assignment);
            assignment.ProgressValue = Math.Min(progress, assignment.DailyQuest.TargetValue);

            var stat = await EnsureDailyQuestStatAsync(userId, assignment.AssignedDate, resetSnapshot: false);
            await UpdateDailyQuestStatAsync(userId, stat);

            if (!assignment.Completed && progress >= assignment.DailyQuest.TargetValue)
            {
                assignment.Completed = true;
                assignment.CompletedAt = DateTime.UtcNow;
            }

            if (assignment.Completed && !assignment.RewardGranted)
            {
                await GrantQuestExperienceAsync(userId, assignment.DailyQuest.RewardExperience);
                assignment.RewardGranted = true;
                stat.CompletedDailyQuests += 1;
                stat.EarnedExperience += assignment.DailyQuest.RewardExperience;
                stat.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return assignment;
        }

        public async Task EvaluateAutomaticChallengesForUserAsync(int userId)
        {
            var participants = await _db.ChallengeParticipants
                .Include(p => p.Challenge)
                .ThenInclude(c => c.Game)
                .Where(p => p.UserId == userId &&
                            p.Status == ChallengeParticipantStatuses.Joined &&
                            p.Challenge.Status == ChallengeStatuses.Approved &&
                            p.Challenge.VerificationType == ChallengeVerificationTypes.Automatic)
                .ToListAsync();

            foreach (var participant in participants)
                await TryCompleteAutomaticChallengeAsync(participant);

            await _db.SaveChangesAsync();
        }

        public async Task<string> CheckAutomaticChallengeAsync(int userId, int challengeId)
        {
            var participant = await _db.ChallengeParticipants
                .Include(p => p.Challenge)
                .ThenInclude(c => c.Game)
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ChallengeId == challengeId);

            if (participant == null)
                return "Сначала нужно участвовать в челлендже.";

            if (participant.Challenge.VerificationType != ChallengeVerificationTypes.Automatic)
                return "Этот челлендж подтверждается вручную.";

            if (participant.Status == ChallengeParticipantStatuses.Completed)
                return "Челлендж уже выполнен.";

            var completed = await TryCompleteAutomaticChallengeAsync(participant);
            await _db.SaveChangesAsync();

            return completed
                ? "Система подтвердила выполнение челленджа."
                : "Пока не выполнено. Синхронизируй профиль после получения нужных достижений.";
        }

        public async Task<string> ApproveManualSubmissionAsync(int submissionId, int reviewerUserId)
        {
            var submission = await _db.ChallengeSubmissions
                .Include(s => s.Challenge)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null)
                return "Заявка не найдена.";

            if (submission.Challenge.CreatedByUserId != reviewerUserId)
                return "Подтверждать выполнение может только создатель челленджа.";

            if (submission.Status != ChallengeSubmissionStatuses.Pending)
                return "Эта заявка уже обработана.";

            var participant = await _db.ChallengeParticipants
                .FirstOrDefaultAsync(p => p.ChallengeId == submission.ChallengeId && p.UserId == submission.UserId);

            if (participant == null)
                return "Участник челленджа не найден.";

            submission.Status = ChallengeSubmissionStatuses.Approved;
            submission.ReviewedAt = DateTime.UtcNow;
            submission.ReviewedByUserId = reviewerUserId;

            await CompleteChallengeParticipantAsync(participant, submission.Challenge.RewardExperience);
            await _notificationService.AddAsync(
                submission.UserId,
                NotificationTypes.Challenge,
                "Челлендж подтвержден",
                $"Создатель подтвердил выполнение челленджа «{submission.Challenge.Title}».",
                $"/Challenges/Details/{submission.ChallengeId}");
            await _db.SaveChangesAsync();

            return "Выполнение челленджа подтверждено.";
        }

        public async Task<string> RejectManualSubmissionAsync(int submissionId, int reviewerUserId)
        {
            var submission = await _db.ChallengeSubmissions
                .Include(s => s.Challenge)
                .FirstOrDefaultAsync(s => s.Id == submissionId);

            if (submission == null)
                return "Заявка не найдена.";

            if (submission.Challenge.CreatedByUserId != reviewerUserId)
                return "Отклонять заявку может только создатель челленджа.";

            if (submission.Status != ChallengeSubmissionStatuses.Pending)
                return "Эта заявка уже обработана.";

            submission.Status = ChallengeSubmissionStatuses.Rejected;
            submission.ReviewedAt = DateTime.UtcNow;
            submission.ReviewedByUserId = reviewerUserId;
            await _db.SaveChangesAsync();

            return "Заявка отклонена.";
        }

        private async Task<bool> TryCompleteAutomaticChallengeAsync(ChallengeParticipant participant)
        {
            if (!participant.Challenge.GameId.HasValue)
                return false;

            var gameId = participant.Challenge.GameId.Value;
            var targetValue = Math.Max(1, participant.Challenge.TargetValue);
            var completed = participant.Challenge.AutoGoalType switch
            {
                ChallengeAutoGoalTypes.EarnAchievementsInGame =>
                    await CountCompletedAchievementsInGameAsync(participant.UserId, gameId, rareOnly: false) >= targetValue,
                ChallengeAutoGoalTypes.EarnRareAchievementsInGame =>
                    await CountCompletedAchievementsInGameAsync(participant.UserId, gameId, rareOnly: true) >= targetValue,
                _ => await HasCompletedGame100Async(participant.UserId, gameId)
            };

            if (!completed)
                return false;

            await CompleteChallengeParticipantAsync(participant, participant.Challenge.RewardExperience);
            return true;
        }

        private async Task CompleteChallengeParticipantAsync(ChallengeParticipant participant, int rewardExperience)
        {
            participant.Status = ChallengeParticipantStatuses.Completed;
            participant.CompletedAt ??= DateTime.UtcNow;

            if (participant.RewardGranted)
                return;

            await GrantQuestExperienceAsync(participant.UserId, rewardExperience);
            participant.RewardGranted = true;
        }

        private async Task GrantQuestExperienceAsync(int userId, int experience)
        {
            if (experience <= 0)
                return;

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return;

            user.QuestExperience += experience;
            await _notificationService.EvaluateProfileMilestonesAsync(user.Id);
        }

        private async Task<int> GetDailyQuestProgressAsync(int userId, DailyQuestAssignment assignment)
        {
            return assignment.DailyQuest.QuestType switch
            {
                DailyQuestTypes.EarnRareAchievements => await CountCompletedAchievementsAsync(userId, assignment.CreatedAt, rareOnly: true),
                DailyQuestTypes.CompleteGame100 => await CountCompletedGames100Async(userId, assignment.CreatedAt),
                _ => await CountCompletedAchievementsAsync(userId, assignment.CreatedAt, rareOnly: false)
            };
        }

        private async Task<int> CountCompletedAchievementsAsync(int userId, DateTime? since = null, bool rareOnly = false)
        {
            var query = _db.UserAchievements
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Completed);

            if (since.HasValue)
                query = query.Where(x => x.UnlockTime >= since.Value);

            if (rareOnly)
                query = query.Where(x => x.Achievement.GlobalUnlockRate > 0 && x.Achievement.GlobalUnlockRate < 10);

            return await query.CountAsync();
        }

        private async Task<int> CountCompletedGames100Async(int userId, DateTime? completedSince = null)
        {
            var totals = await _db.Achievements
                .AsNoTracking()
                .Where(a => !a.IsCustom)
                .GroupBy(a => a.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    Total = g.Count()
                })
                .ToDictionaryAsync(x => x.GameId, x => x.Total);

            if (totals.Count == 0)
                return 0;

            var completed = await _db.UserAchievements
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Completed && !x.Achievement.IsCustom)
                .GroupBy(x => x.Achievement.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    Count = g.Count(),
                    LatestUnlock = g.Max(x => x.UnlockTime)
                })
                .ToListAsync();

            return completed.Count(x =>
                totals.TryGetValue(x.GameId, out var total) &&
                x.Count >= total &&
                (!completedSince.HasValue || x.LatestUnlock >= completedSince.Value));
        }

        private async Task<bool> HasCompletedGame100Async(int userId, int gameId)
        {
            var total = await _db.Achievements
                .AsNoTracking()
                .CountAsync(a => a.GameId == gameId && !a.IsCustom);

            if (total == 0)
                return false;

            var completed = await _db.UserAchievements
                .AsNoTracking()
                .CountAsync(x => x.UserId == userId &&
                                 x.Completed &&
                                 x.Achievement.GameId == gameId &&
                                 !x.Achievement.IsCustom);

            return completed >= total;
        }

        private async Task<int> CountCompletedAchievementsInGameAsync(int userId, int gameId, bool rareOnly)
        {
            var query = _db.UserAchievements
                .AsNoTracking()
                .Where(x => x.UserId == userId &&
                            x.Completed &&
                            x.Achievement.GameId == gameId &&
                            !x.Achievement.IsCustom);

            if (rareOnly)
                query = query.Where(x => x.Achievement.GlobalUnlockRate > 0 && x.Achievement.GlobalUnlockRate < 10);

            return await query.CountAsync();
        }

        private async Task<DailyQuestStat> EnsureDailyQuestStatAsync(int userId, DateTime statDate, bool resetSnapshot)
        {
            var stat = await _db.DailyQuestStats
                .FirstOrDefaultAsync(s => s.UserId == userId && s.StatDate == statDate);

            if (stat == null)
            {
                stat = new DailyQuestStat
                {
                    UserId = userId,
                    StatDate = statDate
                };
                _db.DailyQuestStats.Add(stat);
                resetSnapshot = true;
            }

            if (resetSnapshot)
            {
                stat.StartingCompletedAchievements = await CountCompletedAchievementsAsync(userId);
                stat.StartingRareAchievements = await CountCompletedAchievementsAsync(userId, rareOnly: true);
                stat.StartingCompletedGames100 = await CountCompletedGames100Async(userId);
                stat.TrackedAchievementsGained = 0;
                stat.TrackedRareAchievementsGained = 0;
                stat.TrackedCompletedGames100 = 0;
                stat.UpdatedAt = DateTime.UtcNow;
            }

            return stat;
        }

        private async Task UpdateDailyQuestStatAsync(int userId, DailyQuestStat stat)
        {
            stat.TrackedAchievementsGained = Math.Max(0, await CountCompletedAchievementsAsync(userId) - stat.StartingCompletedAchievements);
            stat.TrackedRareAchievementsGained = Math.Max(0, await CountCompletedAchievementsAsync(userId, rareOnly: true) - stat.StartingRareAchievements);
            stat.TrackedCompletedGames100 = Math.Max(0, await CountCompletedGames100Async(userId) - stat.StartingCompletedGames100);
            stat.UpdatedAt = DateTime.UtcNow;
        }
    }
}
