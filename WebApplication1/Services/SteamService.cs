using System.Net.Http.Json;

public class SteamService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _client;

    public SteamService(IConfiguration config, IHttpClientFactory factory)
    {
        _config = config;
        _client = factory.CreateClient();
    }

    public async Task<Player?> GetProfileAsync(string steamId)
    {
        var key = _config["Steam:ApiKey"];
        var url =
            $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={key}&steamids={steamId}";

        var response = await _client.GetFromJsonAsync<SteamResponse>(url);

        return response?.Response?.Players?.FirstOrDefault();
    }
}

public class SteamResponse
{
    public SteamPlayerResponse Response { get; set; }
}

public class SteamPlayerResponse
{
    public List<Player> Players { get; set; }
}

public class Player
{
    public string Steamid { get; set; }
    public string Personaname { get; set; }
    public string Avatarfull { get; set; }
}