using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using WebApplication1.DTO;
using WebApplication1.Models;
using WebApplication1.Services;

public class GamesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly SteamService _steamService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AdminAccessService _adminAccessService;
    private readonly QuestProgressService _questProgressService;
    private readonly NotificationService _notificationService;

    private IActionResult RedirectToCatalogFeedback()
    {
        return RedirectToAction("Catalog", "Home", null, "catalog-feedback");
    }

    public GamesController(
        ApplicationDbContext context,
        SteamService steamService,
        UserManager<ApplicationUser> userManager,
        AdminAccessService adminAccessService,
        QuestProgressService questProgressService,
        NotificationService notificationService)
    {
        _context = context;
        _steamService = steamService;
        _userManager = userManager;
        _adminAccessService = adminAccessService;
        _questProgressService = questProgressService;
        _notificationService = notificationService;
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";

        return s.Trim().ToLowerInvariant();
    }

    private async Task<bool> HasAdminAccessAsync(ApplicationUser? identityUser = null)
    {
        identityUser ??= await _userManager.GetUserAsync(User);
        if (identityUser == null ||
            HttpContext.Session.GetString("AdminAccess") != "Granted" ||
            HttpContext.Session.GetString("AdminAccessSteamId") != identityUser.SteamId ||
            !await _adminAccessService.IsAllowedAsync(identityUser))
        {
            HttpContext.Session.Remove("AdminAccess");
            HttpContext.Session.Remove("AdminAccessSteamId");
            return false;
        }

        return true;
    }

    private async Task EnsureGameAchievementsLoadedAsync(Game game)
    {
        if (game.Achievements.Any())
            return;

        var schemaAchievementsTask = _steamService.GetAchievementsAsync(game.SteamAppId);
        var globalRatesTask = _steamService.GetGlobalRates(game.SteamAppId);

        var schemaAchievements = await schemaAchievementsTask ?? new List<SteamAchievementDto>();
        if (schemaAchievements.Count == 0)
        {
            await globalRatesTask;
            return;
        }

        var globalRates = await globalRatesTask;

        foreach (var schemaAchievement in schemaAchievements)
        {
            var normalizedApiName = Normalize(schemaAchievement.Name);
            game.Achievements.Add(new Achievement
            {
                Title = schemaAchievement.DisplayName ?? "",
                Description = schemaAchievement.Description ?? "",
                ApiName = schemaAchievement.Name ?? "",
                IconUrl = schemaAchievement.Icon,
                IsHidden = schemaAchievement.IsHidden,
                GlobalUnlockRate = globalRates.TryGetValue(normalizedApiName, out var percent) ? percent : 0
            });
        }

        await _context.SaveChangesAsync();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var game = await _context.Games
            .Include(g => g.Achievements)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
            return NotFound("Игра не найдена");

        if (game.Achievements == null)
            game.Achievements = new List<Achievement>();

        if (!game.Achievements.Any() && game.SteamAppId > 0)
            await EnsureGameAchievementsLoadedAsync(game);

        var identityUser = await _userManager.GetUserAsync(User);
        User? publicUser = null;
        var isAdmin = await HasAdminAccessAsync(identityUser);

        if (identityUser != null && !string.IsNullOrWhiteSpace(identityUser.SteamId))
        {
            publicUser = await _context.Users
                .FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);
        }

        var completedAchievements = new Dictionary<int, DateTime?>();

        if (publicUser != null)
        {
            var completedAchievementRows = await _context.UserAchievements
                .Where(x => x.UserId == publicUser.Id &&
                            x.Completed &&
                            x.Achievement.GameId == game.Id)
                .Select(x => new
                {
                    x.AchievementId,
                    x.UnlockTime
                })
                .ToListAsync();

            completedAchievements = completedAchievementRows
                .GroupBy(x => x.AchievementId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.UnlockTime).Where(x => x.HasValue).Min());
        }

        var pendingCustomClaimAchievementIds = publicUser == null
            ? new HashSet<int>()
            : (await _context.CustomAchievementClaimRequests
                .AsNoTracking()
                .Where(x => x.UserId == publicUser.Id && x.Status == "Pending" && x.Achievement.GameId == game.Id)
                .Select(x => x.AchievementId)
                .ToListAsync())
                .ToHashSet();

        var model = new GameDetailsViewModel
        {
            Game = game,
            TotalAchievements = game.Achievements.Count,
            CompletedAchievements = completedAchievements.Count,
            HiddenAchievements = game.Achievements.Count(a => a.IsHidden),
            CanAddCustomAchievement = identityUser != null,
            CanManageCustomAchievements = isAdmin,
            CustomAchievement = new CustomAchievementInputModel
            {
                GameId = game.Id
            },
            AchievementItems = game.Achievements
                .OrderByDescending(a => completedAchievements.ContainsKey(a.Id))
                .ThenBy(a => a.IsHidden)
                .ThenByDescending(a => a.IsCustom)
                .ThenBy(a => a.Title)
                .Select(a => new GameAchievementItemViewModel
                {
                    AchievementId = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    ObtainMethod = a.ObtainMethod,
                    IconUrl = string.IsNullOrWhiteSpace(a.IconUrl) ? game.AvatarUrl : a.IconUrl,
                    GlobalUnlockRate = a.GlobalUnlockRate,
                    UnlockTime = completedAchievements.TryGetValue(a.Id, out var unlockTime) ? unlockTime : null,
                    IsCompleted = completedAchievements.ContainsKey(a.Id),
                    IsCustom = a.IsCustom,
                    IsHidden = a.IsHidden,
                    HasPendingCompletionRequest = pendingCustomClaimAchievementIds.Contains(a.Id),
                    CanRequestCompletion = a.IsCustom && publicUser != null && !completedAchievements.ContainsKey(a.Id) && !pendingCustomClaimAchievementIds.Contains(a.Id),
                    CanDeleteCustomAchievement = isAdmin && a.IsCustom
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomAchievement(CustomAchievementInputModel input)
    {
        var identityUser = await _userManager.GetUserAsync(User);
        if (identityUser == null)
            return RedirectToAction("Login", "Account");

        var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == input.GameId);
        if (game == null)
            return NotFound("Игра не найдена");

        var title = SteamService.CleanText(input.Title);
        var description = SteamService.CleanText(input.Description);
        var obtainMethod = SteamService.CleanText(input.ObtainMethod);
        var iconUrl = input.IconUrl?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["GameDetailsError"] = "Введите название достижения.";
            return RedirectToAction(nameof(Details), new { id = input.GameId });
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            TempData["GameDetailsError"] = "Введите описание достижения.";
            return RedirectToAction(nameof(Details), new { id = input.GameId });
        }

        if (string.IsNullOrWhiteSpace(obtainMethod))
        {
            TempData["GameDetailsError"] = "Введите способ получения достижения.";
            return RedirectToAction(nameof(Details), new { id = input.GameId });
        }

        if (title.Length > CustomAchievementInputModel.TitleMaxLength ||
            description.Length > CustomAchievementInputModel.DescriptionMaxLength ||
            obtainMethod.Length > CustomAchievementInputModel.ObtainMethodMaxLength ||
            iconUrl.Length > CustomAchievementInputModel.IconUrlMaxLength)
        {
            TempData["GameDetailsError"] = "Поля пользовательского достижения превышают допустимую длину.";
            return RedirectToAction(nameof(Details), new { id = input.GameId });
        }

        if (!string.IsNullOrWhiteSpace(iconUrl) &&
            (!Uri.TryCreate(iconUrl, UriKind.Absolute, out var iconUri) ||
             iconUri.Scheme is not ("http" or "https")))
        {
            TempData["GameDetailsError"] = "Ссылка на иконку должна начинаться с http:// или https://.";
            return RedirectToAction(nameof(Details), new { id = input.GameId });
        }

        var publicUser = await _context.Users.FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);

        var duplicatePendingRequest = await _context.CustomAchievementRequests
            .AnyAsync(x => x.GameId == game.Id &&
                           (x.Status == CustomAchievementRequestStatuses.Pending ||
                            x.Status == CustomAchievementRequestStatuses.Voting) &&
                           x.Title == title);

        if (duplicatePendingRequest)
        {
            TempData["GameDetailsError"] = "Такая заявка на достижение уже ожидает проверки или голосования.";
            return RedirectToAction(nameof(Details), new { id = input.GameId });
        }

        _context.CustomAchievementRequests.Add(new CustomAchievementRequest
        {
            GameId = game.Id,
            Title = title,
            Description = description,
            ObtainMethod = obtainMethod,
            IconUrl = string.IsNullOrWhiteSpace(iconUrl) ? game.AvatarUrl : iconUrl,
            RequestedByUserId = publicUser?.Id,
            CreatedAt = DateTime.UtcNow,
            Status = CustomAchievementRequestStatuses.Pending
        });

        await _context.SaveChangesAsync();

        TempData["GameDetailsSuccess"] = "Заявка на достижение отправлена администраторам.";
        return RedirectToAction(nameof(Details), new { id = input.GameId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCustomAchievementCompletion(int achievementId, string? comment, string? proofUrl)
    {
        var identityUser = await _userManager.GetUserAsync(User);
        if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
            return RedirectToAction("Login", "Account");

        var publicUser = await _context.Users.FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);
        if (publicUser == null)
            return RedirectToAction("Login", "Account");

        var achievement = await _context.Achievements
            .Include(x => x.Game)
            .FirstOrDefaultAsync(x => x.Id == achievementId && x.IsCustom);

        if (achievement == null)
            return View("~/Views/Shared/NotFound.cshtml", "Пользовательское достижение не найдено.");

        var alreadyCompleted = await _context.UserAchievements
            .AnyAsync(x => x.UserId == publicUser.Id && x.AchievementId == achievement.Id && x.Completed);

        if (alreadyCompleted)
        {
            TempData["GameDetailsSuccess"] = "Это достижение уже отмечено полученным.";
            return RedirectToAction(nameof(Details), new { id = achievement.GameId });
        }

        var pendingRequest = await _context.CustomAchievementClaimRequests
            .FirstOrDefaultAsync(x => x.UserId == publicUser.Id &&
                                      x.AchievementId == achievement.Id &&
                                      x.Status == "Pending");

        var cleanedComment = SteamService.CleanText(comment);
        var cleanedProofUrl = (proofUrl ?? "").Trim();

        if (cleanedComment.Length > CustomAchievementClaimRequest.CommentMaxLength)
        {
            TempData["GameDetailsError"] = "Комментарий к доказательству слишком длинный.";
            return RedirectToAction(nameof(Details), new { id = achievement.GameId });
        }

        if (cleanedProofUrl.Length > CustomAchievementClaimRequest.ProofUrlMaxLength)
        {
            TempData["GameDetailsError"] = "Ссылка на доказательство слишком длинная.";
            return RedirectToAction(nameof(Details), new { id = achievement.GameId });
        }

        if (!string.IsNullOrWhiteSpace(cleanedProofUrl) &&
            (!Uri.TryCreate(cleanedProofUrl, UriKind.Absolute, out var proofUri) ||
             proofUri.Scheme is not ("http" or "https")))
        {
            TempData["GameDetailsError"] = "Ссылка на доказательство должна начинаться с http:// или https://.";
            return RedirectToAction(nameof(Details), new { id = achievement.GameId });
        }

        var now = DateTime.UtcNow;
        var userAchievement = await _context.UserAchievements
            .FirstOrDefaultAsync(x => x.UserId == publicUser.Id && x.AchievementId == achievement.Id);

        if (userAchievement == null)
        {
            _context.UserAchievements.Add(new UserAchievement
            {
                UserId = publicUser.Id,
                AchievementId = achievement.Id,
                Completed = true,
                UnlockTime = now,
                IconUrl = achievement.IconUrl
            });
        }
        else
        {
            userAchievement.Completed = true;
            userAchievement.UnlockTime ??= now;
            userAchievement.IconUrl = achievement.IconUrl;
        }

        if (pendingRequest == null)
        {
            _context.CustomAchievementClaimRequests.Add(new CustomAchievementClaimRequest
            {
                AchievementId = achievement.Id,
                UserId = publicUser.Id,
                Comment = cleanedComment,
                ProofUrl = cleanedProofUrl,
                CreatedAt = now,
                Status = "Approved"
            });
        }
        else
        {
            pendingRequest.Comment = cleanedComment;
            pendingRequest.ProofUrl = cleanedProofUrl;
            pendingRequest.Status = "Approved";
        }

        await _context.SaveChangesAsync();

        publicUser.TotalAchievements = await _context.UserAchievements
            .CountAsync(x => x.UserId == publicUser.Id && x.Completed);

        await _questProgressService.EvaluateDailyQuestAsync(publicUser.Id);
        await _questProgressService.EvaluateAutomaticChallengesForUserAsync(publicUser.Id);
        await _notificationService.EvaluateProfileMilestonesAsync(publicUser.Id);
        await _context.SaveChangesAsync();

        TempData["GameDetailsSuccess"] = "Достижение сразу добавлено в профиль.";
        return RedirectToAction(nameof(Details), new { id = achievement.GameId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomAchievement(int achievementId)
    {
        if (!await HasAdminAccessAsync())
            return RedirectToAction("Login", "Admin");

        var achievement = await _context.Achievements
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == achievementId && x.IsCustom);

        if (achievement == null)
            return View("~/Views/Shared/NotFound.cshtml", "Пользовательское достижение не найдено.");

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var affectedUserIds = await _context.UserAchievements
            .Where(x => x.AchievementId == achievement.Id)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync();

        await _context.CustomAchievementClaimRequests
            .Where(x => x.AchievementId == achievement.Id)
            .ExecuteDeleteAsync();

        await _context.UserAchievements
            .Where(x => x.AchievementId == achievement.Id)
            .ExecuteDeleteAsync();

        await _context.Achievements
            .Where(x => x.Id == achievement.Id)
            .ExecuteDeleteAsync();

        if (affectedUserIds.Count > 0)
        {
            var totals = await _context.UserAchievements
                .Where(x => affectedUserIds.Contains(x.UserId) && x.Completed)
                .GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var users = await _context.Users
                .Where(x => affectedUserIds.Contains(x.Id))
                .ToListAsync();

            foreach (var user in users)
                user.TotalAchievements = totals.TryGetValue(user.Id, out var total) ? total : 0;

            await _context.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        TempData["GameDetailsSuccess"] = "Пользовательское достижение удалено.";
        return RedirectToAction(nameof(Details), new { id = achievement.GameId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOwnedLibrary()
    {
        var identityUser = await _userManager.GetUserAsync(User);
        if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
            return RedirectToAction("Login", "Account");

        var publicUser = await _context.Users.FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);

        var ownedGames = await _steamService.GetOwnedGames(identityUser.SteamId);
        if (ownedGames.Count == 0)
        {
            TempData["ErrorMessage"] = "Steam не вернул игры из библиотеки. Проверь публичность профиля и попробуй еще раз.";
            return RedirectToCatalogFeedback();
        }

        var appIds = ownedGames
            .Select(x => x.AppId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var libraryAppIds = appIds.ToHashSet();

        var existingAppIds = await _context.Games
            .AsNoTracking()
            .Select(x => x.SteamAppId)
            .ToListAsync();

        var pendingAppIds = await _context.GameRequests
            .AsNoTracking()
            .Where(x => x.Status == "Pending")
            .Select(x => x.SteamAppId)
            .ToListAsync();

        var skipAppIds = existingAppIds
            .Concat(pendingAppIds)
            .Where(libraryAppIds.Contains)
            .ToHashSet();

        var candidates = ownedGames
            .Where(x => x.AppId > 0 && !skipAppIds.Contains(x.AppId))
            .GroupBy(x => x.AppId)
            .Select(g => g.First())
            .ToList();

        if (candidates.Count == 0)
        {
            TempData["SuccessMessage"] = "Все игры из библиотеки уже есть в каталоге или ожидают модерации.";
            return RedirectToCatalogFeedback();
        }

        using var semaphore = new SemaphoreSlim(4);
        var checkedGames = await Task.WhenAll(candidates.Select(async game =>
        {
            await semaphore.WaitAsync();
            try
            {
                var achievements = await _steamService.GetAchievementsAsync(game.AppId);
                if (achievements.Count == 0)
                    return null;

                var name = SteamService.CleanText(game.Name);
                return new GameRequest
                {
                    SteamAppId = game.AppId,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Steam App {game.AppId}" : name,
                    ImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{game.AppId}/header.jpg",
                    AchievementsCount = achievements.Count,
                    RequestedByUserId = publicUser?.Id,
                    SteamUrl = $"https://store.steampowered.com/app/{game.AppId}",
                    CreatedAt = DateTime.UtcNow,
                    Status = "Pending"
                };
            }
            finally
            {
                semaphore.Release();
            }
        }));

        var requests = checkedGames
            .Where(x => x != null)
            .Cast<GameRequest>()
            .ToList();

        if (requests.Count == 0)
        {
            TempData["ErrorMessage"] = "В библиотеке не найдено новых игр с достижениями для отправки на модерацию.";
            return RedirectToCatalogFeedback();
        }

        _context.GameRequests.AddRange(requests);
        await _context.SaveChangesAsync();

        var skippedWithoutAchievements = candidates.Count - requests.Count;
        TempData["SuccessMessage"] = $"Заявки из библиотеки отправлены: {requests.Count}. Пропущено без достижений: {skippedWithoutAchievements}. Уже были в каталоге или на модерации: {skipAppIds.Count}.";
        return RedirectToCatalogFeedback();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFromSteam(string steamUrl)
    {
        var identityUser = await _userManager.GetUserAsync(User);
        var publicUser = identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId)
            ? null
            : await _context.Users.FirstOrDefaultAsync(x => x.SteamId == identityUser.SteamId);

        if (string.IsNullOrWhiteSpace(steamUrl))
        {
            TempData["ErrorMessage"] = "Введите ссылку на игру Steam.";
            return RedirectToCatalogFeedback();
        }

        if (steamUrl.Length > GameRequest.UrlMaxLength)
        {
            TempData["ErrorMessage"] = "Ссылка на игру Steam слишком длинная.";
            return RedirectToCatalogFeedback();
        }

        var appId = ExtractAppId(steamUrl);
        if (appId == null)
        {
            TempData["ErrorMessage"] = "Не удалось извлечь AppID из ссылки.";
            return RedirectToCatalogFeedback();
        }

        var existingGame = await _context.Games
            .FirstOrDefaultAsync(g => g.SteamAppId == appId.Value);

        if (existingGame != null)
        {
            TempData["ErrorMessage"] = "Эта игра уже есть в каталоге.";
            return RedirectToCatalogFeedback();
        }

        var existingRequest = await _context.GameRequests
            .FirstOrDefaultAsync(r => r.SteamAppId == appId.Value && r.Status == "Pending");

        if (existingRequest != null)
        {
            TempData["ErrorMessage"] = "Заявка на эту игру уже отправлена и ожидает проверки.";
            return RedirectToCatalogFeedback();
        }

        var gameDataTask = _steamService.GetGameDataAsync(appId.Value);
        var achievementsTask = _steamService.GetAchievementsAsync(appId.Value);

        await Task.WhenAll(gameDataTask, achievementsTask);

        var gameData = await gameDataTask;
        if (gameData == null)
        {
            TempData["ErrorMessage"] = "Не удалось получить данные игры из Steam.";
            return RedirectToCatalogFeedback();
        }

        var achievements = await achievementsTask ?? new List<SteamAchievementDto>();

        if (achievements.Count == 0)
        {
            TempData["ErrorMessage"] = "Эта игра не отправлена: Steam не вернул для неё достижений.";
            return RedirectToCatalogFeedback();
        }
        

        var request = new GameRequest
        {
            SteamAppId = appId.Value,
            Name = gameData.Name,
            ImageUrl = gameData.HeaderImage,
            AchievementsCount = achievements.Count,
            RequestedByUserId = publicUser?.Id,
            SteamUrl = steamUrl,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending"
        };

        _context.GameRequests.Add(request);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Заявка отправлена администратору.";
        return RedirectToCatalogFeedback();
    }

    private int? ExtractAppId(string url)
    {
        var match = Regex.Match(url, @"app\/(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var appId) && appId > 0)
            return appId;

        return null;
    }
}
