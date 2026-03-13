using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using WebApplication1.Models;
using WebApplication1.DTO;

public class GamesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly SteamService _steamService;

    public GamesController(ApplicationDbContext context, SteamService steamService)
    {
        _context = context;
        _steamService = steamService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFromSteam(string steamUrl)
    {
        if (string.IsNullOrWhiteSpace(steamUrl))
            return BadRequest("Steam URL пустой");

        var appId = ExtractAppId(steamUrl);
        if (appId == null)
            return BadRequest("Не удалось извлечь AppID");

        var existingGame = _context.Games.FirstOrDefault(g => g.SteamAppId == appId.Value);
        if (existingGame != null)
            return RedirectToAction("Catalog", "Home");

        var gameData = await _steamService.GetGameDataAsync(appId.Value);
        if (gameData == null)
            return BadRequest("Не удалось получить данные игры");

        var achievements = await _steamService.GetAchievementsAsync(appId.Value) ?? new List<SteamAchievementDto>();

        var game = new Game
        {
            SteamAppId = appId.Value,
            Name = gameData.Name,
            Description = gameData.ShortDescription,
            AvatarUrl = gameData.HeaderImage
        };

        foreach (var ach in achievements)
        {
            game.Achievements.Add(new Achievement
            {
                Title = ach.DisplayName ?? "",
                Description = ach.Description ?? "",
                ApiName = ach.Name ?? ""
            });
        }

        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        return RedirectToAction("Catalog", "Home");
    }

    private int? ExtractAppId(string url)
    {
        var match = Regex.Match(url, @"app\/(\d+)");
        if (match.Success)
            return int.Parse(match.Groups[1].Value);

        return null;
    }
}