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

            var gameData = await _steamService.GetGameDataAsync(request.SteamAppId);
            if (gameData == null)
            {
                TempData["AdminError"] = "Не удалось получить данные игры из Steam.";
                return RedirectToAction(nameof(Index));
            }

                var achievements = await _steamService.GetAchievementsAsync(request.SteamAppId)
                               ?? new List<SteamAchievementDto>();

            var game = new Game
            {
                SteamAppId = request.SteamAppId,
                Name = gameData.Name,
                Description = gameData.ShortDescription,
                AvatarUrl = gameData.HeaderImage
            };

            var globalRates = await _steamService.GetGlobalRates(game.SteamAppId);

            foreach (var ach in achievements)
            {
                var key = string.IsNullOrWhiteSpace(ach.Name) ? "" : ach.Name.Trim().ToLowerInvariant();

                game.Achievements.Add(new Achievement
                {
                    Title = ach.DisplayName ?? "",
                    Description = ach.Description ?? "",
                    ApiName = ach.Name ?? "",
                    GlobalUnlockRate = key != null && globalRates.TryGetValue(key, out var percent)
                        ? percent
                        : 0
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