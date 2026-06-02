using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class CustomAchievementVote
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue)]
        public int CustomAchievementRequestId { get; set; }

        public CustomAchievementRequest CustomAchievementRequest { get; set; } = null!;

        [Range(1, int.MaxValue)]
        public int UserId { get; set; }

        public User User { get; set; } = null!;

        public bool IsPositive { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
