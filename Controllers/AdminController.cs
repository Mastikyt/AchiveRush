using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.DTO;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SteamService _steamService;
        private readonly IConfiguration _configuration;

        public AdminController(
            ApplicationDbContext context,
            SteamService steamService,
            IConfiguration configuration)
        {
            _context = context;
            _steamService = steamService;
            _configuration = configuration;
        }

        private bool IsAdminAuthenticated()
        {
            return HttpContext.Session.GetString("AdminAccess") == "Granted";
        }

        private static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "";

            return s.Trim().ToLowerInvariant();
        }

        private async Task<List<SteamAchievementDto>> GetSchemaAchievementsWithRetryAsync(int appId)
        {
            var achievements = await _steamService.GetAchievementsAsync(appId) ?? new List<SteamAchievementDto>();
            if (achievements.Count > 0)
                return achievements;

            await Task.Delay(400);
            return await _steamService.GetAchievementsAsync(appId) ?? new List<SteamAchievementDto>();
        }

        [HttpGet("/secret-admin-login")]
        public IActionResult Login()
        {
            if (IsAdminAuthenticated())
                return RedirectToAction(nameof(Index));

            return View();
        }

        [HttpPost("/secret-admin-login")]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string password)
        {
            var adminPassword = _configuration["AdminSettings:Password"];

            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                ViewBag.Error = "Пароль админки не настроен в appsettings.json";
                return View();
            }

            if (password != adminPassword)
            {
                ViewBag.Error = "Неверный пароль.";
                return View();
            }

            HttpContext.Session.SetString("AdminAccess", "Granted");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("/secret-admin")]
        public async Task<IActionResult> Index()
        {
            if (!IsAdminAuthenticated())
                return RedirectToAction(nameof(Login));

            var requests = await _context.GameRequests
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveGameRequest(int id)
        {
            if (!IsAdminAuthenticated())
                return RedirectToAction(nameof(Login));

            var request = await _context.GameRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
                return NotFound();

            if (request.Status != "Pending")
                return RedirectToAction(nameof(Index));

            var existingGame = await _context.Games
                .FirstOrDefaultAsync(g => g.SteamAppId == request.SteamAppId);

            if (existingGame != null)
            {
                request.Status = "Approved";
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var gameDataTask = _steamService.GetGameDataAsync(request.SteamAppId);
            var schemaAchievementsTask = GetSchemaAchievementsWithRetryAsync(request.SteamAppId);
            var globalRatesTask = _steamService.GetGlobalRates(request.SteamAppId);

            await Task.WhenAll(gameDataTask, schemaAchievementsTask, globalRatesTask);

            var gameData = await gameDataTask;
            if (gameData == null)
            {
                TempData["AdminError"] = "Не удалось получить данные игры из Steam.";
                return RedirectToAction(nameof(Index));
            }

            var schemaAchievements = await schemaAchievementsTask;
            if (schemaAchievements.Count == 0)
            {
                TempData["AdminError"] = "Steam не вернул достижения для этой игры. Попробуй одобрить заявку еще раз позже.";
                return RedirectToAction(nameof(Index));
            }

            var game = new Game
            {
                SteamAppId = request.SteamAppId,
                Name = gameData.Name,
                Description = gameData.ShortDescription,
                AvatarUrl = gameData.HeaderImage,
                Achievements = new List<Achievement>()
            };

            var globalRates = await globalRatesTask;

            foreach (var schemaAchievement in schemaAchievements)
            {
                var normalizedApiName = Normalize(schemaAchievement.Name);

                var existingAchievement = game.Achievements
                    .FirstOrDefault(a => Normalize(a.ApiName) == normalizedApiName);

                if (existingAchievement != null)
                {
                    if (globalRates.TryGetValue(normalizedApiName, out var existingPercent))
                        existingAchievement.GlobalUnlockRate = existingPercent;

                    existingAchievement.Title = schemaAchievement.DisplayName ?? "";
                    existingAchievement.Description = schemaAchievement.Description ?? "";
                    continue;
                }

                game.Achievements.Add(new Achievement
                {
                    Title = schemaAchievement.DisplayName ?? "",
                    Description = schemaAchievement.Description ?? "",
                    ApiName = schemaAchievement.Name ?? "",
                    GlobalUnlockRate = globalRates.TryGetValue(normalizedApiName, out var percent) ? percent : 0
                });
            }

            _context.Games.Add(game);
            request.Status = "Approved";

            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Игра успешно добавлена в каталог.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectGameRequest(int id)
        {
            if (!IsAdminAuthenticated())
                return RedirectToAction(nameof(Login));

            var request = await _context.GameRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
                return NotFound();

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Заявка отклонена.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/secret-admin-logout")]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("AdminAccess");
            return RedirectToAction(nameof(Login));
        }
    }
}
