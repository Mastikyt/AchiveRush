using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public static class CustomAchievementRequestStatuses
    {
        public const string Pending = "Pending";
        public const string Voting = "Voting";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    public class CustomAchievementRequest
    {
        public const int TitleMaxLength = 256;
        public const int DescriptionMaxLength = 2000;
        public const int ObtainMethodMaxLength = 2000;
        public const int IconUrlMaxLength = 2048;
        public const int StatusMaxLength = 32;

        public int Id { get; set; }

        [Range(1, int.MaxValue)]
        public int GameId { get; set; }

        public Game Game { get; set; } = null!;

        public int? RequestedByUserId { get; set; }

        public User? RequestedByUser { get; set; }

        [StringLength(TitleMaxLength)]
        public string Title { get; set; } = "";

        [StringLength(DescriptionMaxLength)]
        public string Description { get; set; } = "";

        [StringLength(ObtainMethodMaxLength)]
        public string ObtainMethod { get; set; } = "";

        [StringLength(IconUrlMaxLength)]
        public string IconUrl { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public DateTime? VotingStartedAt { get; set; }

        public DateTime? VotingEndsAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        [StringLength(StatusMaxLength)]
        public string Status { get; set; } = "Pending";

        public ICollection<CustomAchievementVote> Votes { get; set; } = new List<CustomAchievementVote>();
    }
}
