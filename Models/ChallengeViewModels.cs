using Microsoft.AspNetCore.Http;

namespace WebApplication1.Models
{
    public class ChallengesIndexViewModel
    {
        public bool IsSignedIn { get; set; }

        public List<ChallengeListItemViewModel> Challenges { get; set; } = new();

        public string SearchQuery { get; set; } = "";

        public string SelectedCategory { get; set; } = "";

        public string SelectedType { get; set; } = "";

        public string SelectedDifficulty { get; set; } = "";
    }

    public class ChallengeCreateInputModel
    {
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

        public IFormFile? CoverImage { get; set; }

        public int? GameId { get; set; }

        public int RewardExperience { get; set; } = 100;

        public int ParticipantLimit { get; set; } = 10;
    }

    public class ChallengeCreateViewModel
    {
        public bool IsSignedIn { get; set; }

        public ChallengeCreateInputModel Challenge { get; set; } = new();

        public List<GameOptionViewModel> Games { get; set; } = new();
    }

    public class GameOptionViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";
    }

    public class ChallengeListItemViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string Difficulty { get; set; } = "";

        public string Category { get; set; } = "";

        public string ChallengeType { get; set; } = "";

        public string VerificationType { get; set; } = "";

        public string AutoGoalType { get; set; } = "";

        public int TargetValue { get; set; }

        public string ManualProofDescription { get; set; } = "";

        public string CoverImageUrl { get; set; } = "";

        public int RewardExperience { get; set; }

        public int ParticipantLimit { get; set; }

        public int ParticipantsCount { get; set; }

        public int CompletedCount { get; set; }

        public string GameName { get; set; } = "";

        public string GameAvatarUrl { get; set; } = "";

        public string CreatedByName { get; set; } = "";

        public string? UserParticipantStatus { get; set; }

        public bool UserCanJoin { get; set; }

        public bool UserCanSubmitProof { get; set; }

        public bool UserHasPendingSubmission { get; set; }

        public bool UserCompleted { get; set; }

        public bool IsCreator { get; set; }

        public List<ChallengeSubmissionReviewItemViewModel> PendingSubmissions { get; set; } = new();
    }

    public class ChallengeDetailsViewModel
    {
        public bool IsSignedIn { get; set; }

        public ChallengeListItemViewModel Challenge { get; set; } = new();

        public List<ChallengeParticipantViewModel> Participants { get; set; } = new();
    }

    public class ChallengeParticipantViewModel
    {
        public string UserName { get; set; } = "";

        public string SteamId { get; set; } = "";

        public string AvatarUrl { get; set; } = "";

        public DateTime JoinedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string Status { get; set; } = "";

        public TimeSpan? TimeSpent { get; set; }
    }

    public class ChallengeSubmissionReviewItemViewModel
    {
        public int Id { get; set; }

        public string UserName { get; set; } = "";

        public string ProofUrl { get; set; } = "";

        public string Comment { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }

    public class DailyQuestAssignmentViewModel
    {
        public int AssignmentId { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string Difficulty { get; set; } = "";

        public string QuestType { get; set; } = "";

        public int TargetValue { get; set; }

        public int ProgressValue { get; set; }

        public int RewardExperience { get; set; }

        public bool Completed { get; set; }

        public bool CanReroll { get; set; }
    }

    public class AdminGamesViewModel
    {
        public List<GameRequest> Requests { get; set; } = new();

        public List<AdminGameListItemViewModel> Games { get; set; } = new();

        public int PendingRequestsCount { get; set; }

        public int ApprovedRequestsCount { get; set; }

        public int RejectedRequestsCount { get; set; }

        public int DeletedRequestsCount { get; set; }

        public int RequestsPage { get; set; } = 1;

        public int RequestsTotalPages { get; set; } = 1;

        public int RequestsTotalCount { get; set; }

        public int GamesPage { get; set; } = 1;

        public int GamesTotalPages { get; set; } = 1;

        public int GamesTotalCount { get; set; }
    }

    public class AdminAchievementsViewModel
    {
        public List<CustomAchievementRequestListItemViewModel> CustomAchievementRequests { get; set; } = new();

        public List<CustomAchievementClaimRequestListItemViewModel> CustomAchievementClaimRequests { get; set; } = new();
    }

    public class AdminChallengesViewModel
    {
        public List<AdminChallengeListItemViewModel> Challenges { get; set; } = new();

        public int PendingChallengesCount { get; set; }

        public int ApprovedChallengesCount { get; set; }

        public int RejectedChallengesCount { get; set; }
    }

    public class AdminChallengeListItemViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string Difficulty { get; set; } = "";

        public string Category { get; set; } = "";

        public string ChallengeType { get; set; } = "";

        public string VerificationType { get; set; } = "";

        public string AutoGoalType { get; set; } = "";

        public int TargetValue { get; set; }

        public string CoverImageUrl { get; set; } = "";

        public int RewardExperience { get; set; }

        public int ParticipantLimit { get; set; }

        public int ParticipantsCount { get; set; }

        public string GameName { get; set; } = "";

        public string GameAvatarUrl { get; set; } = "";

        public string CreatedByName { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public string Status { get; set; } = "";
    }

    public class AdminUsersViewModel
    {
        public List<AdminUserListItemViewModel> Users { get; set; } = new();

        public int TotalUsers { get; set; }

        public int UsersPage { get; set; } = 1;

        public int UsersTotalPages { get; set; } = 1;
    }

    public class AdminUserListItemViewModel
    {
        public int Id { get; set; }

        public string SteamId { get; set; } = "";

        public string SteamName { get; set; } = "";

        public string AvatarUrl { get; set; } = "";

        public int TotalAchievements { get; set; }

        public int QuestExperience { get; set; }

        public int CompletedDailyQuests { get; set; }

        public int CompletedChallenges { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? LastSync { get; set; }
    }
}
