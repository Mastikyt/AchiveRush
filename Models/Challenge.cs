namespace WebApplication1.Models
{
    public static class ChallengeStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    public static class ChallengeParticipantStatuses
    {
        public const string Joined = "Joined";
        public const string Completed = "Completed";
    }

    public static class ChallengeSubmissionStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    public static class ChallengeVerificationTypes
    {
        public const string Automatic = "Automatic";
        public const string Manual = "Manual";
    }

    public static class ChallengeAutoGoalTypes
    {
        public const string GameCompletion100 = "GameCompletion100";
        public const string EarnAchievementsInGame = "EarnAchievementsInGame";
        public const string EarnRareAchievementsInGame = "EarnRareAchievementsInGame";

        public static readonly string[] All =
        {
            GameCompletion100,
            EarnAchievementsInGame,
            EarnRareAchievementsInGame
        };
    }

    public static class ChallengeCategories
    {
        public const string Completion = "Completion";
        public const string Speedrun = "Speedrun";
        public const string Collection = "Collection";
        public const string RareHunt = "RareHunt";
        public const string Skill = "Skill";
        public const string Coop = "Coop";
        public const string Hardcore = "Hardcore";
        public const string Discovery = "Discovery";
        public const string Creative = "Creative";
        public const string Community = "Community";

        public static readonly string[] All =
        {
            Completion,
            Speedrun,
            Collection,
            RareHunt,
            Skill,
            Coop,
            Hardcore,
            Discovery,
            Creative,
            Community
        };
    }

    public static class ChallengeTypes
    {
        public const string Completion = "Completion";
        public const string AchievementHunt = "AchievementHunt";
        public const string RareAchievementHunt = "RareAchievementHunt";
        public const string TimeTrial = "TimeTrial";
        public const string Marathon = "Marathon";
        public const string NoDeath = "NoDeath";
        public const string TeamRun = "TeamRun";
        public const string CreativeProof = "CreativeProof";

        public static readonly string[] All =
        {
            Completion,
            AchievementHunt,
            RareAchievementHunt,
            TimeTrial,
            Marathon,
            NoDeath,
            TeamRun,
            CreativeProof
        };
    }

    public static class ChallengeDifficulties
    {
        public const string Easy = "Easy";
        public const string Normal = "Normal";
        public const string Hard = "Hard";
        public const string Legendary = "Legendary";

        public static readonly string[] All = { Easy, Normal, Hard, Legendary };
    }

    public class Challenge
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string Difficulty { get; set; } = ChallengeDifficulties.Normal;

        public string Category { get; set; } = ChallengeCategories.Completion;

        public string ChallengeType { get; set; } = ChallengeTypes.Completion;

        public string VerificationType { get; set; } = ChallengeVerificationTypes.Automatic;

        public string AutoGoalType { get; set; } = ChallengeAutoGoalTypes.GameCompletion100;

        public int TargetValue { get; set; } = 1;

        public string ManualProofDescription { get; set; } = "";

        public string CoverImageUrl { get; set; } = "";

        public int RewardExperience { get; set; }

        public int ParticipantLimit { get; set; }

        public int? GameId { get; set; }

        public Game? Game { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        public string Status { get; set; } = ChallengeStatuses.Pending;

        public ICollection<ChallengeParticipant> Participants { get; set; } = new List<ChallengeParticipant>();

        public ICollection<ChallengeSubmission> Submissions { get; set; } = new List<ChallengeSubmission>();
    }

    public class ChallengeParticipant
    {
        public int Id { get; set; }

        public int ChallengeId { get; set; }

        public Challenge Challenge { get; set; } = null!;

        public int UserId { get; set; }

        public User User { get; set; } = null!;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public string Status { get; set; } = ChallengeParticipantStatuses.Joined;

        public bool RewardGranted { get; set; }
    }

    public class ChallengeSubmission
    {
        public int Id { get; set; }

        public int ChallengeId { get; set; }

        public Challenge Challenge { get; set; } = null!;

        public int UserId { get; set; }

        public User User { get; set; } = null!;

        public string ProofUrl { get; set; } = "";

        public string Comment { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        public int? ReviewedByUserId { get; set; }

        public User? ReviewedByUser { get; set; }

        public string Status { get; set; } = ChallengeSubmissionStatuses.Pending;
    }
}
