using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
public class User
{
    public int Id { get; set; }

    public string SteamId { get; set; }

    public string SteamName { get; set; }

    public DateTime CreatedAt { get; set; }

    public string AvatarID { get; set; }

    public int TotalAchievements { get; set; }

    public DateTime? LastSync { get; set; }

    public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();

    public bool IsProfilePublic { get; set; } = true;
}
    public class UserScore
    {
        public int Id { get; set; }

        public string UserId { get; set; }

        public int Score { get; set; }

        public DateTime DateAchieved { get; set; } = DateTime.UtcNow;


        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
}
