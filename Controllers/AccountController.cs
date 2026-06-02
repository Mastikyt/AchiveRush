using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplication1.Models;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly SteamService _steamService;

    public AccountController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        SteamService steamService)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _steamService = steamService;
    }

    [HttpGet]
    public async Task<IActionResult> SteamResponse(string? returnUrl = null)
    {
        var result = await HttpContext.AuthenticateAsync("Steam");
        if (!result.Succeeded)
            return RedirectToAction("Index", "Home");

        var rawSteamId = result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(rawSteamId))
            return Content("Не удалось получить SteamID.");

        var steamId = rawSteamId.Contains('/') ? rawSteamId.Split('/').Last() : rawSteamId;
        var personaName = result.Principal?.FindFirst(ClaimTypes.Name)?.Value ?? steamId;
        var avatarUrl = result.Principal?.FindFirst("urn:steam:avatarfull")?.Value;

        var steamProfile = await _steamService.GetProfileAsync(steamId);
        if (steamProfile != null)
        {
            if (!string.IsNullOrWhiteSpace(steamProfile.Personaname))
                personaName = steamProfile.Personaname;

            if (!string.IsNullOrWhiteSpace(steamProfile.Avatarfull))
                avatarUrl = steamProfile.Avatarfull;
        }

        if (string.IsNullOrWhiteSpace(avatarUrl))
            avatarUrl = "/img/standart.jpg";

        var identityUser = await _userManager.FindByLoginAsync("Steam", steamId);

        if (identityUser == null)
        {
            identityUser = new ApplicationUser
            {
                UserName = steamId,
                PersonaName = personaName,
                SteamId = steamId,
                AvatarUrl = avatarUrl
            };

            var createResult = await _userManager.CreateAsync(identityUser);
            if (!createResult.Succeeded)
                return Content(string.Join("\n", createResult.Errors.Select(e => e.Description)));

            var loginResult = await _userManager.AddLoginAsync(
                identityUser,
                new UserLoginInfo("Steam", steamId, "Steam"));

            if (!loginResult.Succeeded)
                return Content(string.Join("\n", loginResult.Errors.Select(e => e.Description)));
        }
        else
        {
            identityUser.PersonaName = personaName;
            identityUser.AvatarUrl = avatarUrl;
            identityUser.SteamId = steamId;
            await _userManager.UpdateAsync(identityUser);
        }

        var publicUser = await _context.Users.FirstOrDefaultAsync(x => x.SteamId == steamId);

        if (publicUser == null)
        {
            publicUser = new User
            {
                SteamId = steamId,
                SteamName = personaName,
                AvatarID = avatarUrl,
                CreatedAt = DateTime.UtcNow,
                LastSync = DateTime.MinValue,
                TotalAchievements = 0
            };

            _context.Users.Add(publicUser);
        }
        else
        {
            publicUser.SteamName = personaName;
            publicUser.AvatarID = avatarUrl;
            publicUser.LastSync = DateTime.MinValue;
        }

        await _context.SaveChangesAsync();
        await _signInManager.SignInAsync(identityUser, isPersistent: true);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Profile");
    }

    public IActionResult Login(string? returnUrl = null)
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = Url.Action("SteamResponse", new { returnUrl })
        }, "Steam");
    }

    [HttpGet("/banned")]
    public async Task<IActionResult> Banned()
    {
        var identityUser = await _userManager.GetUserAsync(User);
        var publicUser = identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId)
            ? null
            : await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);

        if (publicUser?.BannedUntil == null || publicUser.BannedUntil <= DateTime.UtcNow)
            return RedirectToAction("Index", "Home");

        return View(new BanStatusViewModel
        {
            BannedUntil = publicUser.BannedUntil,
            Reason = string.IsNullOrWhiteSpace(publicUser.BanReason)
                ? "Причина не указана."
                : publicUser.BanReason
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
