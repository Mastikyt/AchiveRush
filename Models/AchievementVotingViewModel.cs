namespace WebApplication1.Models
{
    public class AchievementVotingViewModel
    {
        public bool IsSignedIn { get; set; }

        public List<AchievementVotingItemViewModel> Items { get; set; } = new();
    }

    public class AchievementVotingItemViewModel
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

        public DateTime? VotingStartedAt { get; set; }

        public DateTime? VotingEndsAt { get; set; }

        public int PositiveVotes { get; set; }

        public int NegativeVotes { get; set; }

        public bool? CurrentUserVote { get; set; }

        public bool CanVote { get; set; }

        public int TotalVotes => PositiveVotes + NegativeVotes;

        public int PositivePercent => TotalVotes == 0
            ? 0
            : (int)Math.Round(PositiveVotes * 100.0 / TotalVotes);
    }
}
