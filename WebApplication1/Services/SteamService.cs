using SteamKit2.WebUI.Internal;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using WebApplication1.DTO;
using WebApplication1.Models;


public class SteamService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _client;

    public SteamService(IConfiguration config, IHttpClientFactory factory)
    {
        _config = config;
        _client = factory.CreateClient();
    }

    // ===== ПРОФИЛЬ =====
    public async Task<Player?> GetProfileAsync(string steamId)
    {
        var key = _config["Steam:ApiKey"];

        var url =
            $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={key}&steamids={steamId}";

        var response = await _client.GetFromJsonAsync<SteamResponse>(url);

        return response?.Response?.Players?.FirstOrDefault();
    }

    // ===== ДАННЫЕ ИГРЫ =====
    public async Task<SteamGameDto?> GetGameDataAsync(int appId)
    {
        var url = $"https://store.steampowered.com/api/appdetails?appids={appId}";

        var response = await _client.GetStringAsync(url);
        var json = JsonDocument.Parse(response);

        if (!json.RootElement.TryGetProperty(appId.ToString(), out var appElement))
            return null;

        if (!appElement.TryGetProperty("data", out var data))
            return null;

        return new SteamGameDto
        {
            Name = data.GetProperty("name").GetString() ?? "",
            HeaderImage = data.GetProperty("header_image").GetString() ?? "",
            ShortDescription = data.GetProperty("short_description").GetString() ?? ""
        };
    }
    public async Task<List<SteamAchievementDto>> GetAchievementsAsync(int appId)
    {
        var key = _config["Steam:ApiKey"];

        var url = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={key}&appid={appId}";

        var response = await _client.GetAsync(url);

        

        if (!response.IsSuccessStatusCode)
            return new List<SteamAchievementDto>();

        var json = await response.Content.ReadAsStringAsync();

        var data = JsonSerializer.Deserialize<SteamSchemaResponse>(json);

        if (data?.game?.availableGameStats?.achievements == null)
            return new List<SteamAchievementDto>();

        return data.game.availableGameStats.achievements
            .Select(a => new SteamAchievementDto
            {
                Name = a.name,
                DisplayName = a.displayName,
                Description = a.description
            })
            .ToList();
    }

    public async Task<List<SteamPlayerAchievement>> GetPlayerAchievements(string steamId, int appId)
    {
        var key = _config["Steam:ApiKey"];

        var url =
            $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={key}&steamid={steamId}&appid={appId}";

        var response = await _client.GetStringAsync(url);

        var json = JsonDocument.Parse(response);

        var list = new List<SteamPlayerAchievement>();

        var achievements = json.RootElement
            .GetProperty("playerstats")
            .GetProperty("achievements");

        foreach (var ach in achievements.EnumerateArray())
        {
            list.Add(new SteamPlayerAchievement
            {
                ApiName = ach.GetProperty("apiname").GetString(),
                Achieved = ach.GetProperty("achieved").GetInt32() == 1
            });
        }

        return list;
    }
}

public class SteamResponse
{
    public SteamPlayerResponse Response { get; set; }
}

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

public class SteamAchievement
{
    public string name { get; set; }

    public string displayName { get; set; }

    public string description { get; set; }
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