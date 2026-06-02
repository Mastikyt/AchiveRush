namespace WebApplication1.Models
{
    public class AdminIndexViewModel
    {
        public List<GameRequest> Requests { get; set; } = new();

        public List<AdminGameListItemViewModel> Games { get; set; } = new();

        public List<CustomAchievementRequestListItemViewModel> CustomAchievementRequests { get; set; } = new();

        public List<CustomAchievementClaimRequestListItemViewModel> CustomAchievementClaimRequests { get; set; } = new();

        public int PendingRequestsCount { get; set; }

        public int ApprovedRequestsCount { get; set; }

        public int RejectedRequestsCount { get; set; }

        public int DeletedRequestsCount { get; set; }

        public int RequestsPage { get; set; } = 1;

        public int RequestsTotalPages { get; set; } = 1;

        public int RequestsTotalCount { get; set; }

        public int RequestsPageSize { get; set; } = 12;

        public string RequestSearch { get; set; } = "";

        public int GamesPage { get; set; } = 1;

        public int GamesTotalPages { get; set; } = 1;

        public int GamesTotalCount { get; set; }

        public int DuplicateGamesCount { get; set; }

        public int GamesPageSize { get; set; } = 12;

        public int PendingCustomAchievementRequestsCount { get; set; }

        public int PendingCustomAchievementClaimRequestsCount { get; set; }
    }

    public class AdminGameListItemViewModel
    {
        public int Id { get; set; }

        public int SteamAppId { get; set; }

        public string Name { get; set; } = "";

        public string AvatarUrl { get; set; } = "";

        public int AchievementsCount { get; set; }

        public int ProfileAchievementsCount { get; set; }
    }

    public class CustomAchievementRequestListItemViewModel
    {
        public int Id { get; set; }

        public int GameId { get; set; }

        public string GameName { get; set; } = "";

        public string GameAvatarUrl { get; set; } = "";

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string ObtainMethod { get; set; } = "";

        public string IconUrl { get; set; } = "";

        public string RequestedBy { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public string Status { get; set; } = "";
    }

    public class CustomAchievementClaimRequestListItemViewModel
    {
        public int Id { get; set; }

        public int AchievementId { get; set; }

        public string AchievementTitle { get; set; } = "";

        public string GameName { get; set; } = "";

        public string GameAvatarUrl { get; set; } = "";

        public string UserName { get; set; } = "";

        public string Comment { get; set; } = "";

        public string ProofUrl { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }
}
