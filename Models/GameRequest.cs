using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class GameRequest
    {
        public int Id { get; set; }

        [Required]
        public int SteamAppId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public int AchievementsCount { get; set; }

        [Required]
        public string SteamUrl { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string Status { get; set; } = "Pending";
    }
}