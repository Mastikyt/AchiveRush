namespace WebApplication1.Models
{
    public class BanStatusViewModel
    {
        public DateTime? BannedUntil { get; set; }

        public string Reason { get; set; } = "";
    }
}
