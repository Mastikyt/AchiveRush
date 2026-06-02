using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Infrastructure
{
    public class BanGuardMiddleware
    {
        private static readonly PathString BannedPath = new("/banned");
        private readonly RequestDelegate _next;

        public BanGuardMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db)
        {
            if (ShouldSkip(context))
            {
                await _next(context);
                return;
            }

            var identityUser = await userManager.GetUserAsync(context.User);
            if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
            {
                await _next(context);
                return;
            }

            var publicUser = await db.Users
                .AsNoTracking()
                .Where(x => x.SteamId == identityUser.SteamId)
                .Select(x => new { x.BannedUntil })
                .FirstOrDefaultAsync();

            if (publicUser?.BannedUntil > DateTime.UtcNow)
            {
                if (IsAjaxRequest(context))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        ok = false,
                        banned = true,
                        redirectUrl = BannedPath.Value
                    });
                    return;
                }

                context.Response.Redirect(BannedPath);
                return;
            }

            await _next(context);
        }

        private static bool ShouldSkip(HttpContext context)
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments(BannedPath) ||
                path.StartsWithSegments("/css") ||
                path.StartsWithSegments("/js") ||
                path.StartsWithSegments("/img") ||
                path.StartsWithSegments("/uploads") ||
                path.StartsWithSegments("/lib") ||
                path.StartsWithSegments("/webfonts") ||
                path.StartsWithSegments("/favicon.ico"))
            {
                return true;
            }

            return path.StartsWithSegments("/Account/Login") ||
                   path.StartsWithSegments("/Account/SteamResponse") ||
                   path.StartsWithSegments("/Account/Logout");
        }

        private static bool IsAjaxRequest(HttpContext context)
        {
            return string.Equals(
                context.Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
