namespace WebApplication1.DTO
{
    public class SteamProfileResponse
    {
        public SteamPlayerResponse response { get; set; }
    }

    public class SteamPlayerResponse
    {
        public List<Player> players { get; set; }
    }
}
