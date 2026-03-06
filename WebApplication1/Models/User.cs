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

        public DateTime LastSync { get; set; }

        public ICollection<UserAchievement> Achievements { get; set; } = new List<UserAchievement>();
    }
}
