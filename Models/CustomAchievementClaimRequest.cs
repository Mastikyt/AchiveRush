namespace WebApplication1.Models
{
    public class CustomAchievementClaimRequest
    {
        public int Id { get; set; }

        public int AchievementId { get; set; }

        public Achievement Achievement { get; set; } = null!;

        public int UserId { get; set; }

        public User User { get; set; } = null!;

        public string Comment { get; set; } = "";

        public string ProofUrl { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "Pending";
    }
}
