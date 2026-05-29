using WebApplication1.Models;

namespace WebApplication1.Services
{
    public static class AchievementLevelService
    {
        public const int CommonXp = 1;
        public const int RareXp = 5;
        public const int EpicXp = 10;
        public const int LegendaryXp = 25;

        public static AchievementLevelInfo Calculate(int legendaryCount, int epicCount, int rareCount, int commonCount)
        {
            var totalXp =
                legendaryCount * LegendaryXp +
                epicCount * EpicXp +
                rareCount * RareXp +
                commonCount * CommonXp;

            return Calculate(totalXp);
        }

        public static AchievementLevelInfo Calculate(int totalXp)
        {
            var level = 1;
            var currentLevelXp = Math.Max(0, totalXp);
            var requiredXp = RequiredXpForNextLevel(level);

            while (currentLevelXp >= requiredXp)
            {
                currentLevelXp -= requiredXp;
                level++;
                requiredXp = RequiredXpForNextLevel(level);
            }

            return new AchievementLevelInfo
            {
                Level = level,
                TotalXp = Math.Max(0, totalXp),
                CurrentLevelXp = currentLevelXp,
                RequiredXp = requiredXp,
                RemainingXp = Math.Max(0, requiredXp - currentLevelXp),
                ProgressPercent = requiredXp == 0 ? 0 : currentLevelXp * 100.0 / requiredXp
            };
        }

        public static int RequiredXpForNextLevel(int level)
        {
            return 100 + Math.Max(1, level) * 5;
        }
    }
}
