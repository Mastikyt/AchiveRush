using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using WebApplication1.DTO;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SteamService _steamService;
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AdminAccessService _adminAccessService;
        private readonly NotificationService _notificationService;
        private readonly QuestProgressService _questProgressService;
        private readonly AchievementSyncService _achievementSyncService;

        public AdminController(
            ApplicationDbContext context,
            SteamService steamService,
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager,
            AdminAccessService adminAccessService,
            NotificationService notificationService,
            QuestProgressService questProgressService,
            AchievementSyncService achievementSyncService)
        {
            _context = context;
            _steamService = steamService;
            _configuration = configuration;
            _userManager = userManager;
            _adminAccessService = adminAccessService;
            _notificationService = notificationService;
            _questProgressService = questProgressService;
            _achievementSyncService = achievementSyncService;
        }

        private async Task<bool> IsAdminAuthenticatedAsync()
        {
            if (HttpContext.Session.GetString("AdminAccess") != "Granted")
                return false;

            var identityUser = await _userManager.GetUserAsync(User);
            if (identityUser == null ||
                HttpContext.Session.GetString("AdminAccessSteamId") != identityUser.SteamId ||
                !await _adminAccessService.IsAllowedAsync(identityUser))
            {
                ClearAdminSession();
                return false;
            }

            return true;
        }

        private void ClearAdminSession()
        {
            HttpContext.Session.Remove("AdminAccess");
            HttpContext.Session.Remove("AdminAccessSteamId");
        }

        private bool IsDynamicAdminRequest()
        {
            return string.Equals(
                Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GameRequestStatusClass(string status)
        {
            return status switch
            {
                "Approved" => "approved",
                "Rejected" => "rejected",
                "Deleted" => "deleted",
                _ => "pending"
            };
        }

        private static string ChallengeStatusClass(string status)
        {
            return status switch
            {
                ChallengeStatuses.Approved => "approved",
                ChallengeStatuses.Rejected => "rejected",
                _ => "pending"
            };
        }

        private IActionResult AdminDynamicOrRedirect(
            string actionName,
            object? routeValues,
            string fragment,
            string? successMessage = null,
            string? errorMessage = null,
            string? status = null,
            string? statusClass = null,
            bool removeCard = false,
            bool disableActions = true)
        {
            if (IsDynamicAdminRequest())
            {
                Response.StatusCode = string.IsNullOrWhiteSpace(errorMessage)
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status400BadRequest;

                return Json(new
                {
                    ok = string.IsNullOrWhiteSpace(errorMessage),
                    message = string.IsNullOrWhiteSpace(errorMessage) ? successMessage : errorMessage,
                    status,
                    statusClass,
                    removeCard,
                    disableActions
                });
            }

            if (!string.IsNullOrWhiteSpace(successMessage))
                TempData["AdminSuccess"] = successMessage;

            if (!string.IsNullOrWhiteSpace(errorMessage))
                TempData["AdminError"] = errorMessage;

            return RedirectToAction(actionName, null, routeValues, fragment);
        }

        private async Task PrepareLoginViewAsync(ApplicationUser? identityUser = null)
        {
            identityUser ??= await _userManager.GetUserAsync(User);
            ViewBag.IsSteamSignedIn = identityUser != null;
            ViewBag.IsAllowedAdminUser = identityUser != null && await _adminAccessService.IsAllowedAsync(identityUser);
            ViewBag.AdminUsersFile = _adminAccessService.GetConfiguredFileName();
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

        private static IQueryable<GameRequest> ApplyGameRequestSearch(IQueryable<GameRequest> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return query;

            var value = search.Trim();
            var hasAppId = int.TryParse(value, out var appId);

            query = query.Where(x =>
                x.Name.Contains(value) ||
                x.SteamUrl.Contains(value) ||
                x.Status.Contains(value) ||
                (hasAppId && x.SteamAppId == appId));

            return query;
        }

        private static string DuplicateKey(string? value)
        {
            return Normalize(value).ToUpperInvariant();
        }

        private async Task<GameRequestProcessResult> ApproveGameRequestCoreAsync(GameRequest request)
        {
            if (request.Status != "Pending")
            {
                return new GameRequestProcessResult(
                    false,
                    "Эта заявка уже обработана.",
                    request.Status,
                    GameRequestStatusClass(request.Status));
            }

            var existingGame = await _context.Games
                .FirstOrDefaultAsync(g => g.SteamAppId == request.SteamAppId);

            if (existingGame != null)
            {
                request.Status = "Approved";
                await _notificationService.AddAsync(
                    request.RequestedByUserId,
                    NotificationTypes.Game,
                    "Игра добавлена",
                    $"Предложенная игра «{existingGame.Name}» уже есть в каталоге.",
                    $"/Games/Details/{existingGame.Id}");
                await _context.SaveChangesAsync();

                return new GameRequestProcessResult(
                    true,
                    $"Игра «{existingGame.Name}» уже была в каталоге, заявка отмечена как одобренная.",
                    request.Status,
                    GameRequestStatusClass(request.Status));
            }

            var gameDataTask = _steamService.GetGameDataAsync(request.SteamAppId);
            var schemaAchievementsTask = GetSchemaAchievementsWithRetryAsync(request.SteamAppId);
            var globalRatesTask = _steamService.GetGlobalRates(request.SteamAppId);

            await Task.WhenAll(gameDataTask, schemaAchievementsTask, globalRatesTask);

            var gameData = await gameDataTask;
            if (gameData == null)
            {
                return new GameRequestProcessResult(
                    false,
                    "Не удалось получить данные игры из Steam.",
                    request.Status,
                    GameRequestStatusClass(request.Status),
                    disableActions: false);
            }

            var schemaAchievements = await schemaAchievementsTask;
            if (schemaAchievements.Count == 0)
            {
                request.Status = "Rejected";
                await _context.SaveChangesAsync();

                return new GameRequestProcessResult(
                    false,
                    "Steam не вернул достижения для этой игры. Заявка отклонена автоматически.",
                    request.Status,
                    GameRequestStatusClass(request.Status));
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
                    existingAchievement.IconUrl = schemaAchievement.Icon;
                    existingAchievement.IsHidden = schemaAchievement.IsHidden;
                    continue;
                }

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

            _context.Games.Add(game);
            request.Status = "Approved";
            await _context.SaveChangesAsync();

            await _notificationService.AddAsync(
                request.RequestedByUserId,
                NotificationTypes.Game,
                "Игра добавлена",
                $"Предложенная игра «{game.Name}» добавлена в каталог.",
                $"/Games/Details/{game.Id}");
            await _context.SaveChangesAsync();

            return new GameRequestProcessResult(
                true,
                "Игра успешно добавлена в каталог.",
                request.Status,
                GameRequestStatusClass(request.Status));
        }

        private async Task<int> RefreshGameLocalizationAsync(Game game)
        {
            var gameDataTask = _steamService.GetGameDataAsync(game.SteamAppId);
            var achievementsTask = GetSchemaAchievementsWithRetryAsync(game.SteamAppId);

            await Task.WhenAll(gameDataTask, achievementsTask);

            var changes = 0;
            var gameData = await gameDataTask;
            if (gameData != null)
            {
                if (!string.IsNullOrWhiteSpace(gameData.Name) && game.Name != gameData.Name)
                {
                    game.Name = gameData.Name;
                    changes++;
                }

                if (!string.IsNullOrWhiteSpace(gameData.ShortDescription) && game.Description != gameData.ShortDescription)
                {
                    game.Description = gameData.ShortDescription;
                    changes++;
                }

                if (!string.IsNullOrWhiteSpace(gameData.HeaderImage) && game.AvatarUrl != gameData.HeaderImage)
                {
                    game.AvatarUrl = gameData.HeaderImage;
                    changes++;
                }
            }

            var localizedAchievements = (await achievementsTask)
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => Normalize(x.Name))
                .ToDictionary(x => x.Key, x => x.First());

            foreach (var achievement in game.Achievements)
            {
                if (!localizedAchievements.TryGetValue(Normalize(achievement.ApiName), out var localized))
                    continue;

                if (!string.IsNullOrWhiteSpace(localized.DisplayName) && achievement.Title != localized.DisplayName)
                {
                    achievement.Title = localized.DisplayName;
                    changes++;
                }

                if (!string.IsNullOrWhiteSpace(localized.Description) && achievement.Description != localized.Description)
                {
                    achievement.Description = localized.Description;
                    changes++;
                }

                if (!string.IsNullOrWhiteSpace(localized.Icon) && achievement.IconUrl != localized.Icon)
                {
                    achievement.IconUrl = localized.Icon;
                    changes++;
                }

                if (achievement.IsHidden != localized.IsHidden)
                {
                    achievement.IsHidden = localized.IsHidden;
                    changes++;
                }
            }

            return changes;
        }

        [HttpGet("/secret-admin-login")]
        public async Task<IActionResult> Login()
        {
            if (await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Index));

            await PrepareLoginViewAsync();
            return View();
        }

        [HttpPost("/secret-admin-login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string password)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            await PrepareLoginViewAsync(identityUser);

            if (identityUser == null)
            {
                ViewBag.Error = "Сначала войди через Steam, потом введи пароль админки.";
                return View();
            }

            if (!await _adminAccessService.IsAllowedAsync(identityUser))
            {
                ViewBag.Error = $"Этот Steam-аккаунт не добавлен в список админов ({ViewBag.AdminUsersFile}).";
                return View();
            }

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
            HttpContext.Session.SetString("AdminAccessSteamId", identityUser.SteamId ?? "");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("/secret-admin")]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Games));
        }

        [HttpGet("/secret-admin/games")]
        public async Task<IActionResult> Games(int requestsPage = 1, int gamesPage = 1, string requestSearch = "")
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            const int requestsPageSize = 12;
            const int gamesPageSize = 12;

            var safeRequestsPage = Math.Max(1, requestsPage);
            var safeGamesPage = Math.Max(1, gamesPage);

            var requestStatusCounts = await _context.GameRequests
                .AsNoTracking()
                .GroupBy(x => x.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            var cleanedRequestSearch = requestSearch?.Trim() ?? "";
            var requestsQuery = ApplyGameRequestSearch(
                _context.GameRequests
                    .AsNoTracking()
                    .Where(x => x.Status != "Deleted"),
                cleanedRequestSearch);

            var requestsTotalCount = await requestsQuery.CountAsync();
            var requestsTotalPages = Math.Max(1, (int)Math.Ceiling(requestsTotalCount / (double)requestsPageSize));
            safeRequestsPage = Math.Min(safeRequestsPage, requestsTotalPages);

            var requests = await requestsQuery
                .OrderByDescending(x => x.CreatedAt)
                .Skip((safeRequestsPage - 1) * requestsPageSize)
                .Take(requestsPageSize)
                .ToListAsync();

            var gamesTotalCount = await _context.Games.AsNoTracking().CountAsync();
            var duplicateGameGroupCounts = await _context.Games
                .AsNoTracking()
                .Where(x => x.SteamAppId > 0)
                .GroupBy(x => x.SteamAppId)
                .Select(g => g.Count())
                .Where(count => count > 1)
                .ToListAsync();

            var duplicateGamesCount = duplicateGameGroupCounts.Sum(count => count - 1);

            var gamesTotalPages = Math.Max(1, (int)Math.Ceiling(gamesTotalCount / (double)gamesPageSize));
            safeGamesPage = Math.Min(safeGamesPage, gamesTotalPages);

            var games = await _context.Games
                .AsNoTracking()
                .OrderBy(g => g.Name)
                .Skip((safeGamesPage - 1) * gamesPageSize)
                .Take(gamesPageSize)
                .Select(g => new AdminGameListItemViewModel
                {
                    Id = g.Id,
                    SteamAppId = g.SteamAppId,
                    Name = g.Name,
                    AvatarUrl = g.AvatarUrl,
                    AchievementsCount = g.Achievements.Count()
                })
                .ToListAsync();

            var visibleGameIds = games.Select(x => x.Id).ToList();

            var profileAchievementCounts = await _context.UserAchievements
                .AsNoTracking()
                .Where(x => visibleGameIds.Contains(x.Achievement.GameId))
                .GroupBy(x => x.Achievement.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.GameId, x => x.Count);

            foreach (var game in games)
            {
                game.ProfileAchievementsCount = profileAchievementCounts.TryGetValue(game.Id, out var count)
                    ? count
                    : 0;
            }

            var customAchievementRequests = await _context.CustomAchievementRequests
                .AsNoTracking()
                .Where(x => x.Status == "Pending")
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CustomAchievementRequestListItemViewModel
                {
                    Id = x.Id,
                    GameId = x.GameId,
                    GameName = x.Game.Name,
                    GameAvatarUrl = x.Game.AvatarUrl,
                    Title = x.Title,
                    Description = x.Description,
                    ObtainMethod = x.ObtainMethod,
                    IconUrl = x.IconUrl,
                    RequestedBy = x.RequestedByUser != null ? x.RequestedByUser.SteamName : "Неизвестно",
                    CreatedAt = x.CreatedAt,
                    Status = x.Status
                })
                .Take(24)
                .ToListAsync();

            var customAchievementClaimRequests = await _context.CustomAchievementClaimRequests
                .AsNoTracking()
                .Where(x => x.Status == "Pending")
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CustomAchievementClaimRequestListItemViewModel
                {
                    Id = x.Id,
                    AchievementId = x.AchievementId,
                    AchievementTitle = x.Achievement.Title,
                    GameName = x.Achievement.Game.Name,
                    GameAvatarUrl = x.Achievement.Game.AvatarUrl,
                    UserName = x.User.SteamName,
                    Comment = x.Comment,
                    ProofUrl = x.ProofUrl,
                    CreatedAt = x.CreatedAt
                })
                .Take(24)
                .ToListAsync();

            return View(new AdminIndexViewModel
            {
                Requests = requests,
                Games = games,
                CustomAchievementRequests = customAchievementRequests,
                CustomAchievementClaimRequests = customAchievementClaimRequests,
                PendingRequestsCount = requestStatusCounts.TryGetValue("Pending", out var pending) ? pending : 0,
                ApprovedRequestsCount = requestStatusCounts.TryGetValue("Approved", out var approved) ? approved : 0,
                RejectedRequestsCount = requestStatusCounts.TryGetValue("Rejected", out var rejected) ? rejected : 0,
                DeletedRequestsCount = requestStatusCounts.TryGetValue("Deleted", out var deleted) ? deleted : 0,
                RequestsPage = safeRequestsPage,
                RequestsPageSize = requestsPageSize,
                RequestsTotalPages = requestsTotalPages,
                RequestsTotalCount = requestsTotalCount,
                RequestSearch = cleanedRequestSearch,
                GamesPage = safeGamesPage,
                GamesPageSize = gamesPageSize,
                GamesTotalPages = gamesTotalPages,
                GamesTotalCount = gamesTotalCount,
                DuplicateGamesCount = duplicateGamesCount,
                PendingCustomAchievementRequestsCount = customAchievementRequests.Count,
                PendingCustomAchievementClaimRequestsCount = customAchievementClaimRequests.Count
            });
        }

        [HttpGet("/secret-admin/achievements")]
        public async Task<IActionResult> Achievements()
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var customAchievementRequests = await _context.CustomAchievementRequests
                .AsNoTracking()
                .Where(x => x.Status == "Pending")
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CustomAchievementRequestListItemViewModel
                {
                    Id = x.Id,
                    GameId = x.GameId,
                    GameName = x.Game.Name,
                    GameAvatarUrl = x.Game.AvatarUrl,
                    Title = x.Title,
                    Description = x.Description,
                    ObtainMethod = x.ObtainMethod,
                    IconUrl = x.IconUrl,
                    RequestedBy = x.RequestedByUser != null ? x.RequestedByUser.SteamName : "Неизвестно",
                    CreatedAt = x.CreatedAt,
                    Status = x.Status
                })
                .Take(48)
                .ToListAsync();

            var customAchievementClaimRequests = await _context.CustomAchievementClaimRequests
                .AsNoTracking()
                .Where(x => x.Status == "Pending")
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CustomAchievementClaimRequestListItemViewModel
                {
                    Id = x.Id,
                    AchievementId = x.AchievementId,
                    AchievementTitle = x.Achievement.Title,
                    GameName = x.Achievement.Game.Name,
                    GameAvatarUrl = x.Achievement.Game.AvatarUrl,
                    UserName = x.User.SteamName,
                    Comment = x.Comment,
                    ProofUrl = x.ProofUrl,
                    CreatedAt = x.CreatedAt
                })
                .Take(48)
                .ToListAsync();

            return View(new AdminAchievementsViewModel
            {
                CustomAchievementRequests = customAchievementRequests,
                CustomAchievementClaimRequests = customAchievementClaimRequests
            });
        }

        [HttpGet("/secret-admin/challenges")]
        public async Task<IActionResult> Challenges()
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var statusCounts = await _context.Challenges
                .AsNoTracking()
                .GroupBy(x => x.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            var challenges = await _context.Challenges
                .AsNoTracking()
                .Include(c => c.Game)
                .Include(c => c.CreatedByUser)
                .Include(c => c.Participants)
                .OrderBy(c => c.Status == ChallengeStatuses.Pending ? 0 : 1)
                .ThenByDescending(c => c.CreatedAt)
                .Select(c => new AdminChallengeListItemViewModel
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    Difficulty = c.Difficulty,
                    Category = c.Category,
                    ChallengeType = c.ChallengeType,
                    VerificationType = c.VerificationType,
                    AutoGoalType = c.AutoGoalType,
                    TargetValue = c.TargetValue,
                    CoverImageUrl = c.CoverImageUrl,
                    RewardExperience = c.RewardExperience,
                    ParticipantLimit = c.ParticipantLimit,
                    ParticipantsCount = c.Participants.Count,
                    GameName = c.Game != null ? c.Game.Name : "",
                    GameAvatarUrl = c.Game != null ? c.Game.AvatarUrl : "",
                    CreatedByName = c.CreatedByUser != null ? c.CreatedByUser.SteamName : "Система",
                    CreatedAt = c.CreatedAt,
                    Status = c.Status
                })
                .Take(80)
                .ToListAsync();

            return View(new AdminChallengesViewModel
            {
                Challenges = challenges,
                PendingChallengesCount = statusCounts.TryGetValue(ChallengeStatuses.Pending, out var pending) ? pending : 0,
                ApprovedChallengesCount = statusCounts.TryGetValue(ChallengeStatuses.Approved, out var approved) ? approved : 0,
                RejectedChallengesCount = statusCounts.TryGetValue(ChallengeStatuses.Rejected, out var rejected) ? rejected : 0
            });
        }

        [HttpGet("/secret-admin/users")]
        public async Task<IActionResult> Users(int usersPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            const int pageSize = 24;
            var safePage = Math.Max(1, usersPage);
            var totalUsers = await _context.Users.AsNoTracking().CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalUsers / (double)pageSize));
            safePage = Math.Min(safePage, totalPages);

            var users = await _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.TotalAchievements)
                .ThenBy(u => u.SteamName)
                .Skip((safePage - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new AdminUserListItemViewModel
                {
                    Id = u.Id,
                    SteamId = u.SteamId,
                    SteamName = u.SteamName,
                    AvatarUrl = u.AvatarID,
                    TotalAchievements = u.TotalAchievements,
                    QuestExperience = u.QuestExperience,
                    CompletedDailyQuests = _context.DailyQuestAssignments.Count(a => a.UserId == u.Id && a.Completed),
                    CompletedChallenges = _context.ChallengeParticipants.Count(p => p.UserId == u.Id && p.Status == ChallengeParticipantStatuses.Completed),
                    CreatedAt = u.CreatedAt,
                    LastSync = u.LastSync,
                    BannedUntil = u.BannedUntil,
                    BanReason = u.BanReason ?? "",
                    IsBanned = u.BannedUntil != null && u.BannedUntil > DateTime.UtcNow
                })
                .ToListAsync();

            return View(new AdminUsersViewModel
            {
                Users = users,
                TotalUsers = totalUsers,
                UsersPage = safePage,
                UsersTotalPages = totalPages
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BanUser(int id, DateTime bannedUntil, string? reason, int usersPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (user == null)
            {
                TempData["AdminError"] = "Пользователь не найден.";
                return RedirectToAction(nameof(Users), new { usersPage });
            }

            var currentAdmin = await _userManager.GetUserAsync(User);
            if (currentAdmin?.SteamId == user.SteamId)
            {
                TempData["AdminError"] = "Нельзя забанить свой аккаунт из админки.";
                return RedirectToAction(nameof(Users), new { usersPage });
            }

            var untilUtc = DateTime.SpecifyKind(bannedUntil, DateTimeKind.Local).ToUniversalTime();
            if (untilUtc <= DateTime.UtcNow)
            {
                TempData["AdminError"] = "Дата окончания бана должна быть в будущем.";
                return RedirectToAction(nameof(Users), new { usersPage });
            }

            user.BannedUntil = untilUtc;
            user.BannedAt = DateTime.UtcNow;
            user.BanReason = string.IsNullOrWhiteSpace(reason)
                ? "Нарушение правил сайта."
                : reason.Trim()[..Math.Min(reason.Trim().Length, 500)];

            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = $"Пользователь «{user.SteamName}» забанен до {user.BannedUntil.Value.ToLocalTime():dd.MM.yyyy HH:mm}.";
            return RedirectToAction(nameof(Users), new { usersPage });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnbanUser(int id, int usersPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (user == null)
            {
                TempData["AdminError"] = "Пользователь не найден.";
                return RedirectToAction(nameof(Users), new { usersPage });
            }

            user.BannedUntil = null;
            user.BannedAt = null;
            user.BanReason = null;
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = $"Пользователь «{user.SteamName}» разбанен.";
            return RedirectToAction(nameof(Users), new { usersPage });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveGameRequest(int id, int requestsPage = 1, int gamesPage = 1, string requestSearch = "")
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var request = await _context.GameRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
            {
                if (IsDynamicAdminRequest())
                    return AdminDynamicOrRedirect(nameof(Games), new { requestsPage, gamesPage, requestSearch }, "game-requests", errorMessage: "Запись не найдена.");

                return View("~/Views/Shared/NotFound.cshtml", "Запись не найдена.");
            }

            var result = await ApproveGameRequestCoreAsync(request);

            return AdminDynamicOrRedirect(
                nameof(Games),
                new { requestsPage, gamesPage, requestSearch },
                "game-requests",
                successMessage: result.Success ? result.Message : null,
                errorMessage: result.Success ? null : result.Message,
                status: result.Status,
                statusClass: result.StatusClass,
                disableActions: result.DisableActions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCustomAchievementRequest(int id, int requestsPage = 1, int gamesPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var request = await _context.CustomAchievementRequests
                .Include(x => x.Game)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (request == null)
                return View("~/Views/Shared/NotFound.cshtml", "Запись не найдена.");

            if (request.Status != CustomAchievementRequestStatuses.Pending)
                return RedirectToAction(nameof(Achievements), null, null, "custom-achievement-requests");

            var duplicate = await _context.Achievements.AnyAsync(x =>
                x.GameId == request.GameId &&
                x.Title == request.Title);

            if (duplicate)
            {
                request.Status = CustomAchievementRequestStatuses.Rejected;
                await _context.SaveChangesAsync();
                TempData["AdminError"] = "Похожее достижение уже есть в этой игре. Заявка отклонена.";
                return RedirectToAction(nameof(Achievements), null, null, "custom-achievement-requests");
            }

            var now = DateTime.UtcNow;
            request.Status = CustomAchievementRequestStatuses.Voting;
            request.VotingStartedAt = now;
            request.VotingEndsAt = CustomAchievementVotingService.GetVotingEnd(now);
            await _notificationService.AddAsync(
                request.RequestedByUserId,
                NotificationTypes.Achievement,
                "Достижение отправлено на голосование",
                $"Предложенное достижение «{request.Title}» прошло модерацию и отправлено на голосование пользователей.",
                "/Home/AchievementVoting");
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = $"Достижение «{request.Title}» отправлено на голосование.";
            return RedirectToAction(nameof(Achievements), null, null, "custom-achievement-requests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCustomAchievementRequest(int id, int requestsPage = 1, int gamesPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var request = await _context.CustomAchievementRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
                return View("~/Views/Shared/NotFound.cshtml", "Запись не найдена.");

            request.Status = CustomAchievementRequestStatuses.Rejected;
            request.ResolvedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Заявка на достижение отклонена.";
            return RedirectToAction(nameof(Achievements), null, null, "custom-achievement-requests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectGameRequest(int id, int requestsPage = 1, int gamesPage = 1, string requestSearch = "")
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var request = await _context.GameRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
            {
                if (IsDynamicAdminRequest())
                    return AdminDynamicOrRedirect(nameof(Games), new { requestsPage, gamesPage, requestSearch }, "game-requests", errorMessage: "Запись не найдена.");

                return View("~/Views/Shared/NotFound.cshtml", "Запись не найдена.");
            }

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            return AdminDynamicOrRedirect(
                nameof(Games),
                new { requestsPage, gamesPage, requestSearch },
                "game-requests",
                successMessage: "Заявка отклонена.",
                status: request.Status,
                statusClass: GameRequestStatusClass(request.Status));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveAllGameRequests(int requestsPage = 1, int gamesPage = 1, string requestSearch = "")
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var pendingRequests = await ApplyGameRequestSearch(
                    _context.GameRequests.Where(x => x.Status == "Pending"),
                    requestSearch)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            var approved = 0;
            var failed = 0;

            foreach (var request in pendingRequests)
            {
                var result = await ApproveGameRequestCoreAsync(request);
                if (result.Success)
                    approved++;
                else
                    failed++;
            }

            TempData["AdminSuccess"] = $"Массовая обработка завершена. Одобрено: {approved}. Ошибок/отклонений: {failed}.";
            return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage, requestSearch }, "game-requests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectAllGameRequests(int requestsPage = 1, int gamesPage = 1, string requestSearch = "")
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var rejected = await ApplyGameRequestSearch(
                    _context.GameRequests.Where(x => x.Status == "Pending"),
                    requestSearch)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Rejected"));

            TempData["AdminSuccess"] = $"Отклонено заявок: {rejected}.";
            return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage, requestSearch }, "game-requests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearApprovedGameRequests(int requestsPage = 1, int gamesPage = 1, string requestSearch = "")
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var cleared = await ApplyGameRequestSearch(
                    _context.GameRequests.Where(x => x.Status == "Approved" || x.Status == "Rejected"),
                    requestSearch)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Deleted"));

            TempData["AdminSuccess"] = $"Скрыто обработанных заявок из админки: {cleared}.";
            return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage, requestSearch }, "game-requests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCustomAchievementClaimRequest(int id, int requestsPage = 1, int gamesPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var request = await _context.CustomAchievementClaimRequests
                .Include(x => x.Achievement)
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (request == null)
                return View("~/Views/Shared/NotFound.cshtml", "Заявка на получение достижения не найдена.");

            if (request.Status != "Pending")
                return RedirectToAction(nameof(Achievements), null, null, "custom-claim-requests");

            var existing = await _context.UserAchievements
                .FirstOrDefaultAsync(x => x.UserId == request.UserId && x.AchievementId == request.AchievementId);

            if (existing == null)
            {
                _context.UserAchievements.Add(new UserAchievement
                {
                    UserId = request.UserId,
                    AchievementId = request.AchievementId,
                    Completed = true,
                    UnlockTime = DateTime.UtcNow,
                    IconUrl = request.Achievement.IconUrl
                });
            }
            else
            {
                existing.Completed = true;
                existing.UnlockTime ??= DateTime.UtcNow;
                existing.IconUrl = request.Achievement.IconUrl;
            }

            request.Status = "Approved";
            await _context.SaveChangesAsync();

            request.User.TotalAchievements = await _context.UserAchievements
                .CountAsync(x => x.UserId == request.UserId && x.Completed);
            await _notificationService.AddAsync(
                request.UserId,
                NotificationTypes.Achievement,
                "Достижение подтверждено",
                $"Получение достижения «{request.Achievement.Title}» подтверждено.",
                $"/Games/Details/{request.Achievement.GameId}");
            await _notificationService.EvaluateProfileMilestonesAsync(request.UserId);
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Получение пользовательского достижения подтверждено.";
            return RedirectToAction(nameof(Achievements), null, null, "custom-claim-requests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectCustomAchievementClaimRequest(int id, int requestsPage = 1, int gamesPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var request = await _context.CustomAchievementClaimRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
                return View("~/Views/Shared/NotFound.cshtml", "Заявка на получение достижения не найдена.");

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Заявка на получение достижения отклонена.";
            return RedirectToAction(nameof(Achievements), null, null, "custom-claim-requests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshGameLocalization(int id, int requestsPage = 1, int gamesPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var game = await _context.Games
                .Include(x => x.Achievements)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (game == null)
            {
                return AdminDynamicOrRedirect(
                    nameof(Games),
                    new { requestsPage, gamesPage },
                    "admin-games",
                    errorMessage: "Игра не найдена.");
            }

            var changes = await RefreshGameLocalizationAsync(game);
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = changes > 0
                ? $"Русская локализация обновлена для игры «{game.Name}». Изменений: {changes}."
                : $"Для игры «{game.Name}» не найдено новых русских данных.";

            return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "admin-games");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshAllGameLocalizations(int requestsPage = 1, int gamesPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var games = await _context.Games
                .Include(x => x.Achievements)
                .Where(x => x.SteamAppId > 0)
                .OrderBy(x => x.Name)
                .ToListAsync();

            var changes = 0;
            foreach (var game in games)
                changes += await RefreshGameLocalizationAsync(game);

            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = $"Обновление русской локализации завершено. Игр: {games.Count}, изменений: {changes}.";
            return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "admin-games");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDuplicateGames(int requestsPage = 1, int gamesPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var catalogGames = await _context.Games
                .AsNoTracking()
                .Where(x => x.SteamAppId > 0)
                .Select(x => new { x.Id, x.SteamAppId })
                .ToListAsync();

            var duplicateGroups = catalogGames
                .GroupBy(x => x.SteamAppId)
                .Where(g => g.Count() > 1)
                .Select(g => new
                {
                    SteamAppId = g.Key,
                    GameIds = g
                        .OrderBy(x => x.Id)
                        .Select(x => x.Id)
                        .ToList()
                })
                .ToList();

            if (duplicateGroups.Count == 0)
            {
                TempData["AdminSuccess"] = "Дубликатов игр в каталоге не найдено.";
                return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "admin-games");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var affectedUserIds = new HashSet<int>();
            var removedGames = 0;
            var removedAchievements = 0;
            var removedProfileAchievements = 0;
            var transferredProfileAchievements = 0;

            foreach (var group in duplicateGroups)
            {
                var keepGameId = group.GameIds[0];
                var duplicateGameIds = group.GameIds.Skip(1).ToList();

                var keptAchievements = await _context.Achievements
                    .Where(x => x.GameId == keepGameId)
                    .ToListAsync();

                foreach (var duplicateGameId in duplicateGameIds)
                {
                    var duplicateAchievements = await _context.Achievements
                        .AsNoTracking()
                        .Where(x => x.GameId == duplicateGameId)
                        .OrderBy(x => x.Id)
                        .ToListAsync();

                    foreach (var duplicateAchievement in duplicateAchievements)
                    {
                        var duplicateApiName = DuplicateKey(duplicateAchievement.ApiName);
                        var duplicateTitle = DuplicateKey(duplicateAchievement.Title);

                        var targetAchievement = keptAchievements.FirstOrDefault(x =>
                            !string.IsNullOrEmpty(duplicateApiName) &&
                            DuplicateKey(x.ApiName) == duplicateApiName);

                        targetAchievement ??= keptAchievements.FirstOrDefault(x =>
                            !string.IsNullOrEmpty(duplicateTitle) &&
                            DuplicateKey(x.Title) == duplicateTitle);

                        if (targetAchievement == null)
                        {
                            targetAchievement = new Achievement
                            {
                                GameId = keepGameId,
                                Title = duplicateAchievement.Title,
                                Description = duplicateAchievement.Description,
                                ApiName = duplicateAchievement.ApiName,
                                IconUrl = duplicateAchievement.IconUrl,
                                ObtainMethod = duplicateAchievement.ObtainMethod,
                                IsCustom = duplicateAchievement.IsCustom,
                                IsHidden = duplicateAchievement.IsHidden,
                                CreatedByUserId = duplicateAchievement.CreatedByUserId,
                                CreatedAt = duplicateAchievement.CreatedAt,
                                GlobalUnlockRate = duplicateAchievement.GlobalUnlockRate
                            };

                            _context.Achievements.Add(targetAchievement);
                            await _context.SaveChangesAsync();
                            keptAchievements.Add(targetAchievement);
                        }

                        var profileAchievements = await _context.UserAchievements
                            .Where(x => x.AchievementId == duplicateAchievement.Id)
                            .ToListAsync();

                        var targetUserIds = await _context.UserAchievements
                            .Where(x => x.AchievementId == targetAchievement.Id)
                            .Select(x => x.UserId)
                            .ToListAsync();

                        var targetUserIdSet = targetUserIds.ToHashSet();

                        foreach (var profileAchievement in profileAchievements)
                        {
                            affectedUserIds.Add(profileAchievement.UserId);

                            if (targetUserIdSet.Contains(profileAchievement.UserId))
                            {
                                _context.UserAchievements.Remove(profileAchievement);
                                removedProfileAchievements++;
                            }
                            else
                            {
                                profileAchievement.AchievementId = targetAchievement.Id;
                                targetUserIdSet.Add(profileAchievement.UserId);
                                transferredProfileAchievements++;
                            }
                        }

                        await _context.SaveChangesAsync();
                    }

                    removedProfileAchievements += await _context.UserAchievements
                        .Where(x => x.Achievement.GameId == duplicateGameId)
                        .ExecuteDeleteAsync();

                    removedAchievements += await _context.Achievements
                        .Where(x => x.GameId == duplicateGameId)
                        .ExecuteDeleteAsync();

                    await _context.Games
                        .Where(x => x.Id == duplicateGameId)
                        .ExecuteDeleteAsync();

                    removedGames++;
                }

                await _context.GameRequests
                    .Where(x => x.SteamAppId == group.SteamAppId && x.Status == "Approved")
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Deleted"));
            }

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
                {
                    user.TotalAchievements = totals.TryGetValue(user.Id, out var total)
                        ? total
                        : 0;
                }

                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            TempData["AdminSuccess"] = $"Дубликаты очищены. Удалено игр: {removedGames}, достижений: {removedAchievements}, перенесено записей профилей: {transferredProfileAchievements}, удалено дублей записей профилей: {removedProfileAchievements}.";
            return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "admin-games");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGame(int id, int requestsPage = 1, int gamesPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var game = await _context.Games
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (game == null)
            {
                TempData["AdminError"] = "Игра не найдена.";
                return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "admin-games");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var affectedUserIds = await _context.UserAchievements
                .Where(x => x.Achievement.GameId == id)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();

            var deletedProfileAchievements = await _context.UserAchievements
                .Where(x => x.Achievement.GameId == id)
                .ExecuteDeleteAsync();

            var deletedAchievements = await _context.Achievements
                .Where(x => x.GameId == id)
                .ExecuteDeleteAsync();

            await _context.Games
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync();

            await _context.GameRequests
                .Where(x => x.SteamAppId == game.SteamAppId && x.Status == "Approved")
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Deleted"));

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
                {
                    user.TotalAchievements = totals.TryGetValue(user.Id, out var total)
                        ? total
                        : 0;
                }

                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return AdminDynamicOrRedirect(
                nameof(Games),
                new { requestsPage, gamesPage },
                "admin-games",
                successMessage: $"Игра «{game.Name}» удалена. Удалено достижений: {deletedAchievements}, записей в профилях: {deletedProfileAchievements}.",
                removeCard: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveChallenge(int id)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var challenge = await _context.Challenges.FirstOrDefaultAsync(x => x.Id == id);
            if (challenge == null)
            {
                if (IsDynamicAdminRequest())
                    return AdminDynamicOrRedirect(nameof(Challenges), null, "admin-challenges", errorMessage: "Челлендж не найден.");

                return View("~/Views/Shared/NotFound.cshtml", "Челлендж не найден.");
            }

            if (challenge.Status != ChallengeStatuses.Pending)
                return AdminDynamicOrRedirect(
                    nameof(Challenges),
                    null,
                    "admin-challenges",
                    errorMessage: "Этот челлендж уже обработан.",
                    status: challenge.Status,
                    statusClass: ChallengeStatusClass(challenge.Status));

            challenge.Status = ChallengeStatuses.Approved;
            challenge.ReviewedAt = DateTime.UtcNow;
            await _notificationService.AddAsync(
                challenge.CreatedByUserId,
                NotificationTypes.Challenge,
                "Челлендж опубликован",
                $"Челлендж «{challenge.Title}» одобрен и появился на странице челленджей.",
                $"/Challenges/Details/{challenge.Id}");
            await _context.SaveChangesAsync();

            return AdminDynamicOrRedirect(
                nameof(Challenges),
                null,
                "admin-challenges",
                successMessage: "Челлендж одобрен и опубликован.",
                status: challenge.Status,
                statusClass: ChallengeStatusClass(challenge.Status));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectChallenge(int id)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var challenge = await _context.Challenges.FirstOrDefaultAsync(x => x.Id == id);
            if (challenge == null)
            {
                if (IsDynamicAdminRequest())
                    return AdminDynamicOrRedirect(nameof(Challenges), null, "admin-challenges", errorMessage: "Челлендж не найден.");

                return View("~/Views/Shared/NotFound.cshtml", "Челлендж не найден.");
            }

            challenge.Status = ChallengeStatuses.Rejected;
            challenge.ReviewedAt = DateTime.UtcNow;
            await _notificationService.AddAsync(
                challenge.CreatedByUserId,
                NotificationTypes.Challenge,
                "Челлендж отклонен",
                $"Челлендж «{challenge.Title}» не прошел модерацию.",
                "/Challenges");
            await _context.SaveChangesAsync();

            return AdminDynamicOrRedirect(
                nameof(Challenges),
                null,
                "admin-challenges",
                successMessage: "Челлендж отклонен.",
                status: challenge.Status,
                statusClass: ChallengeStatusClass(challenge.Status));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChallenge(int id)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var challenge = await _context.Challenges
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (challenge == null)
                return AdminDynamicOrRedirect(nameof(Challenges), null, "admin-challenges", errorMessage: "Челлендж не найден.");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var rewardedParticipants = await _context.ChallengeParticipants
                .Where(x => x.ChallengeId == id && x.RewardGranted)
                .GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Experience = g.Count() * challenge.RewardExperience
                })
                .ToListAsync();

            if (rewardedParticipants.Count > 0)
            {
                var rewardedUserIds = rewardedParticipants.Select(x => x.UserId).ToList();
                var users = await _context.Users
                    .Where(x => rewardedUserIds.Contains(x.Id))
                    .ToListAsync();

                foreach (var user in users)
                {
                    var experience = rewardedParticipants
                        .Where(x => x.UserId == user.Id)
                        .Sum(x => x.Experience);

                    user.QuestExperience = Math.Max(0, user.QuestExperience - experience);
                }

                await _context.SaveChangesAsync();
            }

            var deletedSubmissions = await _context.ChallengeSubmissions
                .Where(x => x.ChallengeId == id)
                .ExecuteDeleteAsync();

            var deletedParticipants = await _context.ChallengeParticipants
                .Where(x => x.ChallengeId == id)
                .ExecuteDeleteAsync();

            await _context.Challenges
                .Where(x => x.Id == id)
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();

            return AdminDynamicOrRedirect(
                nameof(Challenges),
                null,
                "admin-challenges",
                successMessage: $"Челлендж «{challenge.Title}» удален. Участников: {deletedParticipants}, заявок: {deletedSubmissions}.",
                removeCard: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncAllUsers()
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var users = await _context.Users
                .AsNoTracking()
                .Where(u => !string.IsNullOrWhiteSpace(u.SteamId))
                .OrderBy(u => u.Id)
                .Select(u => new
                {
                    u.Id,
                    u.SteamName
                })
                .ToListAsync();

            var synced = 0;
            var failed = 0;

            foreach (var user in users)
            {
                try
                {
                    await _achievementSyncService.SyncAchievementsForUserAsync(user.Id, force: true);
                    await _questProgressService.EvaluateDailyQuestAsync(user.Id);
                    await _questProgressService.EvaluateAutomaticChallengesForUserAsync(user.Id);
                    await _notificationService.EvaluateProfileMilestonesAsync(user.Id);
                    await _context.SaveChangesAsync();
                    synced++;
                }
                catch
                {
                    failed++;
                }
            }

            TempData["AdminSuccess"] = $"Синхронизация пользователей завершена. Успешно: {synced}.";
            if (failed > 0)
                TempData["AdminError"] = $"Не удалось синхронизировать пользователей: {failed}.";

            return RedirectToAction(nameof(Users));
        }

        [HttpPost("/secret-admin-logout")]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            ClearAdminSession();
            return RedirectToAction(nameof(Login));
        }

        private sealed class GameRequestProcessResult
        {
            public GameRequestProcessResult(
                bool success,
                string message,
                string status,
                string statusClass,
                bool disableActions = true)
            {
                Success = success;
                Message = message;
                Status = status;
                StatusClass = statusClass;
                DisableActions = disableActions;
            }

            public bool Success { get; }

            public string Message { get; }

            public string Status { get; }

            public string StatusClass { get; }

            public bool DisableActions { get; }
        }
    }
}
