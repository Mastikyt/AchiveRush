using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using WebApplication1.DTO;
using WebApplication1.Models;

public class GamesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly SteamService _steamService;
    private readonly UserManager<ApplicationUser> _userManager;

    public GamesController(
        ApplicationDbContext context,
        SteamService steamService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _steamService = steamService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var game = await _context.Games
            .Include(g => g.Achievements)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
            return NotFound("Игра не найдена");

        var identityUser = await _userManager.GetUserAsync(User);
        User? publicUser = null;

        if (identityUser != null && !string.IsNullOrWhiteSpace(identityUser.SteamId))
        {
            publicUser = await _context.Users
                .FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);
        }

        var completedAchievementIds = new HashSet<int>();

        if (publicUser != null)
        {
            completedAchievementIds = await _context.UserAchievements
                .Where(x => x.UserId == publicUser.Id &&
                            x.Completed &&
                            x.Achievement.GameId == game.Id)
                .Select(x => x.AchievementId)
                .ToHashSetAsync();
        }

        var model = new GameDetailsViewModel
        {
            Game = game,
            TotalAchievements = game.Achievements.Count,
            CompletedAchievements = completedAchievementIds.Count,
            AchievementItems = game.Achievements
                .Select(a => new GameAchievementItemViewModel
                {
                    AchievementId = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    IsCompleted = completedAchievementIds.Contains(a.Id)
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFromSteam(string steamUrl)
    {
        if (string.IsNullOrWhiteSpace(steamUrl))
        {
            TempData["ErrorMessage"] = "Введите ссылку на игру Steam.";
            return RedirectToAction("Catalog", "Home");
        }

        var appId = ExtractAppId(steamUrl);
        if (appId == null)
        {
            TempData["ErrorMessage"] = "Не удалось извлечь AppID из ссылки.";
            return RedirectToAction("Catalog", "Home");
        }

        var existingGame = await _context.Games
            .FirstOrDefaultAsync(g => g.SteamAppId == appId.Value);

        if (existingGame != null)
        {
            TempData["ErrorMessage"] = "Эта игра уже есть в каталоге.";
            return RedirectToAction("Catalog", "Home");
        }

        var existingRequest = await _context.GameRequests
            .FirstOrDefaultAsync(r => r.SteamAppId == appId.Value && r.Status == "Pending");

        if (existingRequest != null)
        {
            TempData["ErrorMessage"] = "Заявка на эту игру уже отправлена и ожидает проверки.";
            return RedirectToAction("Catalog", "Home");
        }

        var gameData = await _steamService.GetGameDataAsync(appId.Value);
        if (gameData == null)
        {
            TempData["ErrorMessage"] = "Не удалось получить данные игры из Steam.";
            return RedirectToAction("Catalog", "Home");
        }

        var achievements = await _steamService.GetAchievementsAsync(appId.Value)
                           ?? new List<SteamAchievementDto>();

        var request = new GameRequest
        {
            SteamAppId = appId.Value,
            Name = gameData.Name,
            ImageUrl = gameData.HeaderImage,
            AchievementsCount = achievements.Count,
            SteamUrl = steamUrl,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending"
        };

        _context.GameRequests.Add(request);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Заявка отправлена администратору.";
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