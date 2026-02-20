using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

public class AccountController : Controller
{
    private readonly SteamService _steamService;
    private readonly SteamUserManager _steamUserManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(
        SteamService steamService,
        SteamUserManager steamUserManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _steamService = steamService;
        _steamUserManager = steamUserManager;
        _signInManager = signInManager;
    }

    public IActionResult Login()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = Url.Action("SteamCallback")
        }, "Steam");
    }

    public async Task<IActionResult> SteamCallback()
    {
        var steamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (steamId == null)
            return RedirectToAction("Index", "Home");

        var profile = await _steamService.GetProfileAsync(steamId);
        var appUser = await _steamUserManager.CreateOrUpdateAsync(profile);

        await _signInManager.SignInAsync(appUser, isPersistent: true);

        return RedirectToAction("Profile");
    }

    public IActionResult Profile()
    {
        return View();
    }

    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}