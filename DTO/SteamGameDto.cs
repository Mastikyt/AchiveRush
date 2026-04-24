using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.DTO
{
    public class SteamGameDto
    {
        public string Name { get; set; } = "";
        public string HeaderImage { get; set; } = "";
        public string ShortDescription { get; set; } = "";
    }
}
