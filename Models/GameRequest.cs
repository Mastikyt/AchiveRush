using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class GameRequest
    {
        public const int NameMaxLength = 256;
        public const int UrlMaxLength = 2048;
        public const int StatusMaxLength = 32;

        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int SteamAppId { get; set; }

        [Required]
        [StringLength(NameMaxLength)]
        public string Name { get; set; } = string.Empty;

        [StringLength(UrlMaxLength)]
        public string ImageUrl { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int AchievementsCount { get; set; }

        public int? RequestedByUserId { get; set; }

        public User? RequestedByUser { get; set; }

        [Required]
        [StringLength(UrlMaxLength)]
        public string SteamUrl { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(StatusMaxLength)]
        public string Status { get; set; } = "Pending";
    }
}
