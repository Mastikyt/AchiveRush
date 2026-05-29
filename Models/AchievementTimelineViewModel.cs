namespace WebApplication1.Models
{
    public class AchievementTimelineViewModel
    {
        public List<AchievementTimelineSeriesViewModel> Series { get; set; } = new();
    }

    public class AchievementTimelineSeriesViewModel
    {
        public string Key { get; set; } = "";

        public string Label { get; set; } = "";

        public string Description { get; set; } = "";

        public int Total { get; set; }

        public int MaxCount { get; set; }

        public List<AchievementTimelinePointViewModel> Points { get; set; } = new();
    }

    public class AchievementTimelinePointViewModel
    {
        public string Label { get; set; } = "";

        public string ShortLabel { get; set; } = "";

        public int Count { get; set; }

        public double Percent { get; set; }
    }
}
