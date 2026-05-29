namespace WebApplication1.Models
{
    public static class NotificationTypes
    {
        public const string Achievement = "Achievement";
        public const string Game = "Game";
        public const string Challenge = "Challenge";
        public const string Level = "Level";
        public const string Leaderboard = "Leaderboard";
    }

    public class Notification
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public User User { get; set; } = null!;

        public string Type { get; set; } = NotificationTypes.Achievement;

        public string Title { get; set; } = "";

        public string Message { get; set; } = "";

        public string Url { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }
    }
}
