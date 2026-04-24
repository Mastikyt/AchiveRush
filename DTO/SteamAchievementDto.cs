using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.DTO
{
    public class SteamAchievementDto
    {
        public string Name { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public string Description { get; set; } = "";

        public string? Icon { get; set; }

        public double Percent { get; set; }
    }
}
