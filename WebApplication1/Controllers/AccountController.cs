using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SteamKit2;
using System.Security.Claims;
using WebApplication1.Models;

public class AccountController : Controller
{

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
    }



    [HttpGet]
    public async Task<IActionResult> SteamResponse()
    {
        var result = await HttpContext.AuthenticateAsync("Steam");

        if (!result.Succeeded)
            return RedirectToAction("Index", "Home");

        // Получаем SteamID
        var rawSteamId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (rawSteamId == null)
            return Content("Не удалось получить SteamID.");

        var steamId = rawSteamId.Split('/').Last();

        // Проверяем, есть ли пользователь в базе
        var user = await _userManager.FindByLoginAsync("Steam", steamId);

        if (user == null)
        {
            // Получаем ник и аватар из Steam
            var personaName = result.Principal.FindFirst(ClaimTypes.Name)?.Value;
            var avatarUrl = result.Principal.FindFirst("urn:steam:avatarfull")?.Value;

            user = new ApplicationUser
            {
                UserName = personaName ?? steamId,        // уникальное имя в Identity
                PersonaName = personaName ?? steamId,    // ник для фронта
                SteamId = steamId,
                AvatarUrl = avatarUrl ?? "/images/default_avatar.png"
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("\n", createResult.Errors.Select(e => e.Description));
                return Content(errors);
            }

            var loginResult = await _userManager.AddLoginAsync(user, new UserLoginInfo("Steam", steamId, "Steam"));
            if (!loginResult.Succeeded)
            {
                var errors = string.Join("\n", loginResult.Errors.Select(e => e.Description));
                return Content(errors);
            }
        }

        await _signInManager.SignInAsync(user, true);
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Login()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = Url.Action("SteamResponse")
        }, "Steam");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Разлогиниваем пользователя из Identity
        await _signInManager.SignOutAsync();

        return RedirectToAction("Index", "Home");
    }

    

}