using WebApplication1.Models;

using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class GameDetailsViewModel
    {
        public Game Game { get; set; } = null!;
        public int TotalAchievements { get; set; }
        public int CompletedAchievements { get; set; }
        public int HiddenAchievements { get; set; }
        public bool CanAddCustomAchievement { get; set; }
        public bool CanManageCustomAchievements { get; set; }
        public List<GameAchievementItemViewModel> AchievementItems { get; set; } = new();
        public CustomAchievementInputModel CustomAchievement { get; set; } = new();
    }

    public class GameAchievementItemViewModel
    {
        public int AchievementId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string ObtainMethod { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public double GlobalUnlockRate { get; set; }
        public DateTime? UnlockTime { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsCustom { get; set; }
        public bool IsHidden { get; set; }
        public bool HasPendingCompletionRequest { get; set; }
        public bool CanRequestCompletion { get; set; }
        public bool CanDeleteCustomAchievement { get; set; }
    }

    public class CustomAchievementInputModel
    {
        public const int TitleMaxLength = 140;
        public const int DescriptionMaxLength = 1000;
        public const int ObtainMethodMaxLength = 1000;
        public const int IconUrlMaxLength = 2048;

        [Range(1, int.MaxValue)]
        public int GameId { get; set; }

        [StringLength(TitleMaxLength)]
        public string Title { get; set; } = "";

        [StringLength(DescriptionMaxLength)]
        public string Description { get; set; } = "";

        [StringLength(ObtainMethodMaxLength)]
        public string ObtainMethod { get; set; } = "";

        [StringLength(IconUrlMaxLength)]
        public string IconUrl { get; set; } = "";
    }
}
