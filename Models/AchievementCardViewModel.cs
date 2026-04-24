namespace WebApplication1.Models
{
    public class AchievementCardViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string GameName { get; set; } = "";

        public string GameAvatarUrl { get; set; } = "";

        public string? IconUrl { get; set; }

        public DateTime? UnlockTime { get; set; }

        public double GlobalUnlockRate { get; set; }
    }
}
