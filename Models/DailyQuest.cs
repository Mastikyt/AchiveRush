namespace WebApplication1.Models
{
    public static class DailyQuestTypes
    {
        public const string EarnAchievements = "EarnAchievements";
        public const string EarnRareAchievements = "EarnRareAchievements";
        public const string CompleteGame100 = "CompleteGame100";
    }

    public class DailyQuest
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string Difficulty { get; set; } = ChallengeDifficulties.Easy;

        public string QuestType { get; set; } = DailyQuestTypes.EarnAchievements;

        public int TargetValue { get; set; }

        public int RewardExperience { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<DailyQuestAssignment> Assignments { get; set; } = new List<DailyQuestAssignment>();
    }

    public class DailyQuestAssignment
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public User User { get; set; } = null!;

        public int DailyQuestId { get; set; }

        public DailyQuest DailyQuest { get; set; } = null!;

        public DateTime AssignedDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RerolledAt { get; set; }

        public int ProgressValue { get; set; }

        public bool Completed { get; set; }

        public DateTime? CompletedAt { get; set; }

        public bool RewardGranted { get; set; }
    }

    public class DailyQuestStat
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public User User { get; set; } = null!;

        public DateTime StatDate { get; set; }

        public int StartingCompletedAchievements { get; set; }

        public int StartingRareAchievements { get; set; }

        public int StartingCompletedGames100 { get; set; }

        public int TrackedAchievementsGained { get; set; }

        public int TrackedRareAchievementsGained { get; set; }

        public int TrackedCompletedGames100 { get; set; }

        public int CompletedDailyQuests { get; set; }

        public int EarnedExperience { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
