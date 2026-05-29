namespace WebApplication1.Models
{
    public class LeaderboardViewModel
    {
        public List<LeaderboardEntryViewModel> AchievementLeaders { get; set; } = new();

        public List<LeaderboardEntryViewModel> LevelLeaders { get; set; } = new();

        public List<LeaderboardEntryViewModel> WeeklyAchievementLeaders { get; set; } = new();

        public LeaderboardEntryViewModel? CurrentAchievementEntry { get; set; }

        public LeaderboardEntryViewModel? CurrentLevelEntry { get; set; }

        public LeaderboardEntryViewModel? CurrentWeeklyAchievementEntry { get; set; }

        public bool IsSignedIn { get; set; }
    }

    public class LeaderboardEntryViewModel
    {
        public int Rank { get; set; }

        public int UserId { get; set; }

        public string SteamId { get; set; } = "";

        public string SteamName { get; set; } = "";

        public string AvatarUrl { get; set; } = "";

        public int TotalAchievements { get; set; }

        public int WeeklyAchievements { get; set; }

        public int Level { get; set; }

        public int TotalXp { get; set; }

        public int CurrentLevelXp { get; set; }

        public int RequiredXp { get; set; }

        public double ProgressPercent { get; set; }

        public bool IsCurrentUser { get; set; }

        public LeaderboardEntryViewModel WithRank(int rank)
        {
            return new LeaderboardEntryViewModel
            {
                Rank = rank,
                UserId = UserId,
                SteamId = SteamId,
                SteamName = SteamName,
                AvatarUrl = AvatarUrl,
                TotalAchievements = TotalAchievements,
                WeeklyAchievements = WeeklyAchievements,
                Level = Level,
                TotalXp = TotalXp,
                CurrentLevelXp = CurrentLevelXp,
                RequiredXp = RequiredXp,
                ProgressPercent = ProgressPercent,
                IsCurrentUser = IsCurrentUser
            };
        }
    }
}
