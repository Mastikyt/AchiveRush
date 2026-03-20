using WebApplication1.Models;

namespace WebApplication1.Models
{
    public class GameDetailsViewModel
    {
        public Game Game { get; set; } = null!;
        public int TotalAchievements { get; set; }
        public int CompletedAchievements { get; set; }
        public List<GameAchievementItemViewModel> AchievementItems { get; set; } = new();
    }

    public class GameAchievementItemViewModel
    {
        public int AchievementId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsCompleted { get; set; }
    }
}