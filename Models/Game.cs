namespace WebApplication1.Models
{
    public class Game
    {
        public int Id { get; set; }

        public int SteamAppId { get; set; }

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public string AvatarUrl { get; set; } = "";

        public ICollection<Achievement> Achievements { get; set; } = new List<Achievement>();
    }
    

    public class Achievement
    {
        public bool Completed { get; set; }

        public int Id { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string ApiName { get; set; } = "";

        public int GameId { get; set; }

        public Game Game { get; set; }

        public double GlobalUnlockRate { get; set; } // %
    }
}
