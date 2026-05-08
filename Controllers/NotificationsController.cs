using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Open(int id)
        {
            var user = await GetCurrentPublicUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);

            if (notification == null)
                return View("~/Views/Shared/NotFound.cshtml", "Уведомление не найдено.");

            notification.ReadAt ??= DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(notification.Url) &&
                Uri.TryCreate(notification.Url, UriKind.Relative, out _))
            {
                return Redirect(notification.Url);
            }

            return RedirectToAction("Index", "Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var user = await GetCurrentPublicUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            await _context.Notifications
                .Where(n => n.UserId == user.Id && n.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, DateTime.UtcNow));

            var returnUrl = Request.Headers.Referer.ToString();
            return Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearAll()
        {
            var user = await GetCurrentPublicUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .ExecuteDeleteAsync();

            var returnUrl = Request.Headers.Referer.ToString();
            return Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
        }

        private async Task<User?> GetCurrentPublicUserAsync()
        {
            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.SteamId == identityUser.SteamId);
        }
    }
}
