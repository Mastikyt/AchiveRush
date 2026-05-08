namespace WebApplication1.Models
{
    public class AchievementLevelInfo
    {
        public int Level { get; set; }

        public int TotalXp { get; set; }

        public int CurrentLevelXp { get; set; }

        public int RequiredXp { get; set; }

        public int RemainingXp { get; set; }

        public double ProgressPercent { get; set; }
    }
}
