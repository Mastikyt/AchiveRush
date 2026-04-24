using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using WebApplication1.DTO;
using WebApplication1.Models;

public class SteamService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _client;
    private const string SteamLanguage = "russian";
    private const string SteamCountry = "ru";

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";

        return s.Trim().ToLowerInvariant();
    }

    public SteamService(IConfiguration config, IHttpClientFactory factory)
    {
        _config = config;
        _client = factory.CreateClient();
        _client.Timeout = TimeSpan.FromSeconds(20);
        _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
    }

    public async Task<Player?> GetProfileAsync(string steamId)
    {
        try
        {
            var key = _config["Steam:ApiKey"];
            var url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={key}&steamids={steamId}";
            var response = await _client.GetFromJsonAsync<SteamResponse>(url);
            return response?.Response?.Players?.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<SteamGameDto?> GetGameDataAsync(int appId)
    {
        try
        {
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l={SteamLanguage}&cc={SteamCountry}";
            var response = await _client.GetStringAsync(url);
            using var json = JsonDocument.Parse(response);

            if (!json.RootElement.TryGetProperty(appId.ToString(), out var appElement))
                return null;

            if (!appElement.TryGetProperty("data", out var data))
                return null;

            return new SteamGameDto
            {
                Name = data.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                HeaderImage = data.TryGetProperty("header_image", out var image) ? image.GetString() ?? "" : "",
                ShortDescription = data.TryGetProperty("short_description", out var desc) ? desc.GetString() ?? "" : ""
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<SteamAchievementDto>> GetAchievementsAsync(int appId)
    {
        try
        {
            var key = _config["Steam:ApiKey"];
            var url = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={key}&appid={appId}&l={SteamLanguage}";

            using var httpResponse = await _client.GetAsync(url);
            if (!httpResponse.IsSuccessStatusCode)
                return new List<SteamAchievementDto>();

            var response = await httpResponse.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<SteamSchemaResponse>(response);

            if (data?.game?.availableGameStats?.achievements == null)
                return new List<SteamAchievementDto>();

            return data.game.availableGameStats.achievements
                .Where(a => !string.IsNullOrWhiteSpace(a.name))
                .Select(a => new SteamAchievementDto
                {
                    Name = a.name ?? "",
                    DisplayName = a.displayName ?? a.name ?? "",
                    Description = a.description ?? "",
                    Icon = a.icon
                })
                .ToList();
        }
        catch
        {
            return new List<SteamAchievementDto>();
        }
    }

    public async Task<List<SteamPlayerAchievement>> GetPlayerAchievements(string steamId, int appId)
    {
        try
        {
            var key = _config["Steam:ApiKey"];
            var url = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={key}&steamid={steamId}&appid={appId}&l={SteamLanguage}";

            using var httpResponse = await _client.GetAsync(url);
            if (!httpResponse.IsSuccessStatusCode)
                return new List<SteamPlayerAchievement>();

            var response = await httpResponse.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(response);

            var list = new List<SteamPlayerAchievement>();

            if (!json.RootElement.TryGetProperty("playerstats", out var playerStats))
                return list;

            if (!playerStats.TryGetProperty("success", out var successElement))
                return list;

            var success = successElement.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => successElement.GetInt32() == 1,
                _ => false
            };

            if (!success)
                return list;

            if (!playerStats.TryGetProperty("achievements", out var achievementsElement) ||
                achievementsElement.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var ach in achievementsElement.EnumerateArray())
            {
                var apiName = ach.TryGetProperty("apiname", out var apiNameEl)
                    ? apiNameEl.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(apiName))
                    continue;

                var achieved = false;
                if (ach.TryGetProperty("achieved", out var achievedEl))
                {
                    achieved = achievedEl.ValueKind == JsonValueKind.Number
                        ? achievedEl.GetInt32() == 1
                        : achievedEl.ValueKind == JsonValueKind.True;
                }

                list.Add(new SteamPlayerAchievement
                {
                    ApiName = apiName,
                    Achieved = achieved
                });
            }

            return list;
        }
        catch
        {
            return new List<SteamPlayerAchievement>();
        }
    }

    public async Task<Dictionary<string, double>> GetGlobalRates(int appId)
    {
        try
        {
            var url = $"https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/?gameid={appId}";
            using var httpResponse = await _client.GetAsync(url);

            if (!httpResponse.IsSuccessStatusCode)
                return new Dictionary<string, double>();

            var response = await httpResponse.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(response);

            var result = new Dictionary<string, double>();

            if (!json.RootElement.TryGetProperty("achievementpercentages", out var percentages))
                return result;

            if (!percentages.TryGetProperty("achievements", out var achievements))
                return result;

            foreach (var ach in achievements.EnumerateArray())
            {
                var name = ach.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                double percent = 0;
                if (ach.TryGetProperty("percent", out var percentElement))
                {
                    if (percentElement.ValueKind == JsonValueKind.Number)
                        percent = percentElement.GetDouble();
                    else if (percentElement.ValueKind == JsonValueKind.String)
                        double.TryParse(percentElement.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out percent);
                }

                result[Normalize(name)] = percent;
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, double>();
        }
    }

    public async Task<List<SteamOwnedGame>> GetOwnedGames(string steamId)
    {
        try
        {
            var key = _config["Steam:ApiKey"];
            var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={key}&steamid={steamId}&include_appinfo=1&include_played_free_games=1&l={SteamLanguage}";
            var response = await _client.GetFromJsonAsync<SteamOwnedGamesResponse>(url);
            return response?.Response?.Games ?? new List<SteamOwnedGame>();
        }
        catch
        {
            return new List<SteamOwnedGame>();
        }
    }
}

public class SteamResponse
{
    public SteamPlayerResponse? Response { get; set; }
}

public class SteamSchemaResponse
{
    public SteamGame? game { get; set; }
}

public class SteamGame
{
    public SteamStats? availableGameStats { get; set; }
}

public class SteamStats
{
    public List<SteamAchievement>? achievements { get; set; }
}

public class SteamAchievement
{
    public string? name { get; set; }
    public string? displayName { get; set; }
    public string? description { get; set; }
    public string? icon { get; set; }
    public double? percent { get; set; }
}

public class SteamPlayerResponse
{
    public List<Player>? Players { get; set; }
}

public class Player
{
    public string? Steamid { get; set; }
    public string? Personaname { get; set; }
    public string? Avatarfull { get; set; }
}

public class SteamOwnedGamesResponse
{
    [JsonPropertyName("response")]
    public OwnedGamesResponse? Response { get; set; }
}

public class OwnedGamesResponse
{
    [JsonPropertyName("games")]
    public List<SteamOwnedGame>? Games { get; set; }
}

public class SteamOwnedGame
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }

    [JsonPropertyName("playtime_forever")]
    public int PlaytimeForever { get; set; }
}
