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
            return BadRequest("Steam URL пустой");

        var appId = ExtractAppId(steamUrl);
        if (appId == null)
            return BadRequest("Не удалось извлечь AppID");

        var existingGame = await _context.Games
            .Include(g => g.Achievements)
            .FirstOrDefaultAsync(g => g.SteamAppId == appId.Value);

        if (existingGame != null)
            return RedirectToAction("Catalog", "Home");

        var gameData = await _steamService.GetGameDataAsync(appId.Value);
        if (gameData == null)
            return BadRequest("Не удалось получить данные игры");

        var achievements = await _steamService.GetAchievementsAsync(appId.Value)
                           ?? new List<SteamAchievementDto>();

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

        await SyncNewGameForAllUsersAsync(game.Id);

        return RedirectToAction("Catalog", "Home");
    }

    private int? ExtractAppId(string url)
    {
        var match = Regex.Match(url, @"app\/(\d+)");
        if (match.Success)
            return int.Parse(match.Groups[1].Value);

        return null;
    }

    private async Task SyncNewGameForAllUsersAsync(int gameId)
    {
        var game = await _context.Games
            .Include(g => g.Achievements)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null || game.SteamAppId <= 0 || !game.Achievements.Any())
            return;

        var users = await _context.Users.ToListAsync();

        foreach (var user in users)
        {
            List<SteamPlayerAchievement> steamAchievements;

            try
            {
                steamAchievements = await _steamService.GetPlayerAchievements(user.SteamId, game.SteamAppId);
            }
            catch
            {
                continue;
            }

            if (steamAchievements.Count == 0)
                continue;

            foreach (var steamAch in steamAchievements)
            {
                var dbAchievement = game.Achievements.FirstOrDefault(a => a.ApiName == steamAch.ApiName);
                if (dbAchievement == null)
                    continue;

                var userAchievement = await _context.UserAchievements
                    .FirstOrDefaultAsync(ua => ua.UserId == user.Id && ua.AchievementId == dbAchievement.Id);

                if (userAchievement == null)
                {
                    userAchievement = new UserAchievement
                    {
                        UserId = user.Id,
                        AchievementId = dbAchievement.Id,
                        Completed = steamAch.Achieved,
                        UnlockTime = steamAch.Achieved ? DateTime.UtcNow : null
                    };

                    _context.UserAchievements.Add(userAchievement);
                }
                else
                {
                    userAchievement.Completed = steamAch.Achieved;

                    if (steamAch.Achieved && userAchievement.UnlockTime == null)
                        userAchievement.UnlockTime = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            user.TotalAchievements = await _context.UserAchievements
                .CountAsync(x => x.UserId == user.Id && x.Completed);

            user.LastSync = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}