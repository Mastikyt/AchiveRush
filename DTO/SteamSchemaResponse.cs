using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.DTO
{
    public class SteamSchemaResponse
    {
        public SteamGame game { get; set; }
    }

    public class SteamGame
    {
        public SteamStats availableGameStats { get; set; }
    }

    public class SteamStats
    {
        public List<SteamAchievement> achievements { get; set; }
    }
}
