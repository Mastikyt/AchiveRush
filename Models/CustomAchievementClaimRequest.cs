using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class CustomAchievementClaimRequest
    {
        public const int CommentMaxLength = 2000;
        public const int ProofUrlMaxLength = 2048;
        public const int StatusMaxLength = 32;

        public int Id { get; set; }

        [Range(1, int.MaxValue)]
        public int AchievementId { get; set; }

        public Achievement Achievement { get; set; } = null!;

        [Range(1, int.MaxValue)]
        public int UserId { get; set; }

        public User User { get; set; } = null!;

        [StringLength(CommentMaxLength)]
        public string Comment { get; set; } = "";

        [StringLength(ProofUrlMaxLength)]
        public string ProofUrl { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(StatusMaxLength)]
        public string Status { get; set; } = "Pending";
    }
}
