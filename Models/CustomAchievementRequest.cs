namespace WebApplication1.Models
{
    public class CustomAchievementRequest
    {
        public int Id { get; set; }

        public int GameId { get; set; }

        public Game Game { get; set; } = null!;

        public int? RequestedByUserId { get; set; }

        public User? RequestedByUser { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string ObtainMethod { get; set; } = "";

        public string IconUrl { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public string Status { get; set; } = "Pending";
    }
}
