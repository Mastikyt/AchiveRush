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
        public async Task<IActionResult> Games(int requestsPage = 1, int gamesPage = 1)
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

            var requestsTotalCount = await _context.GameRequests.AsNoTracking().CountAsync();
            var requestsTotalPages = Math.Max(1, (int)Math.Ceiling(requestsTotalCount / (double)requestsPageSize));
            safeRequestsPage = Math.Min(safeRequestsPage, requestsTotalPages);

            var requests = await _context.GameRequests
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Skip((safeRequestsPage - 1) * requestsPageSize)
                .Take(requestsPageSize)
                .ToListAsync();

            var gamesTotalCount = await _context.Games.AsNoTracking().CountAsync();
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
                GamesPage = safeGamesPage,
                GamesPageSize = gamesPageSize,
                GamesTotalPages = gamesTotalPages,
                GamesTotalCount = gamesTotalCount,
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
                    LastSync = u.LastSync
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
        public async Task<IActionResult> ApproveGameRequest(int id, int requestsPage = 1, int gamesPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var request = await _context.GameRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
                return View("~/Views/Shared/NotFound.cshtml", "Запись не найдена.");

            if (request.Status != "Pending")
                return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "game-requests");

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
                return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "game-requests");
            }

            var gameDataTask = _steamService.GetGameDataAsync(request.SteamAppId);
            var schemaAchievementsTask = GetSchemaAchievementsWithRetryAsync(request.SteamAppId);
            var globalRatesTask = _steamService.GetGlobalRates(request.SteamAppId);

            await Task.WhenAll(gameDataTask, schemaAchievementsTask, globalRatesTask);

            var gameData = await gameDataTask;
            if (gameData == null)
            {
                TempData["AdminError"] = "Не удалось получить данные игры из Steam.";
                return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "game-requests");
            }

            var schemaAchievements = await schemaAchievementsTask;
            if (schemaAchievements.Count == 0)
            {
                request.Status = "Rejected";
                await _context.SaveChangesAsync();
                TempData["AdminError"] = "Steam не вернул достижения для этой игры. Заявка отклонена автоматически.";
                return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "game-requests");
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
                    continue;
                }

                game.Achievements.Add(new Achievement
                {
                    Title = schemaAchievement.DisplayName ?? "",
                    Description = schemaAchievement.Description ?? "",
                    ApiName = schemaAchievement.Name ?? "",
                    IconUrl = schemaAchievement.Icon,
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

            TempData["AdminSuccess"] = "Игра успешно добавлена в каталог.";
            return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "game-requests");
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

            if (request.Status != "Pending")
                return RedirectToAction(nameof(Achievements), null, null, "custom-achievement-requests");

            var duplicate = await _context.Achievements.AnyAsync(x =>
                x.GameId == request.GameId &&
                x.Title == request.Title);

            if (duplicate)
            {
                request.Status = "Rejected";
                await _context.SaveChangesAsync();
                TempData["AdminError"] = "Похожее достижение уже есть в этой игре. Заявка отклонена.";
                return RedirectToAction(nameof(Achievements), null, null, "custom-achievement-requests");
            }

            _context.Achievements.Add(new Achievement
            {
                GameId = request.GameId,
                Title = request.Title,
                Description = request.Description,
                ObtainMethod = request.ObtainMethod,
                IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? request.Game.AvatarUrl : request.IconUrl,
                ApiName = $"custom:{request.GameId}:{Guid.NewGuid():N}",
                IsCustom = true,
                CreatedByUserId = request.RequestedByUserId,
                CreatedAt = DateTime.UtcNow,
                GlobalUnlockRate = 0
            });

            request.Status = "Approved";
            await _notificationService.AddAsync(
                request.RequestedByUserId,
                NotificationTypes.Achievement,
                "Достижение добавлено",
                $"Предложенное достижение «{request.Title}» добавлено в игру «{request.Game.Name}».",
                $"/Games/Details/{request.GameId}");
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = $"Достижение «{request.Title}» добавлено в игру «{request.Game.Name}».";
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

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Заявка на достижение отклонена.";
            return RedirectToAction(nameof(Achievements), null, null, "custom-achievement-requests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectGameRequest(int id, int requestsPage = 1, int gamesPage = 1)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var request = await _context.GameRequests.FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
                return View("~/Views/Shared/NotFound.cshtml", "Запись не найдена.");

            request.Status = "Rejected";
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Заявка отклонена.";
            return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "game-requests");
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
                TempData["AdminError"] = "Игра не найдена.";
                return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "admin-games");
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

            TempData["AdminSuccess"] = $"Игра «{game.Name}» удалена. Удалено достижений: {deletedAchievements}, записей в профилях: {deletedProfileAchievements}.";
            return RedirectToAction(nameof(Games), null, new { requestsPage, gamesPage }, "admin-games");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveChallenge(int id)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var challenge = await _context.Challenges.FirstOrDefaultAsync(x => x.Id == id);
            if (challenge == null)
                return View("~/Views/Shared/NotFound.cshtml", "Челлендж не найден.");

            if (challenge.Status != ChallengeStatuses.Pending)
                return RedirectToAction(nameof(Challenges), null, null, "admin-challenges");

            challenge.Status = ChallengeStatuses.Approved;
            challenge.ReviewedAt = DateTime.UtcNow;
            await _notificationService.AddAsync(
                challenge.CreatedByUserId,
                NotificationTypes.Challenge,
                "Челлендж опубликован",
                $"Челлендж «{challenge.Title}» одобрен и появился на странице челленджей.",
                $"/Challenges/Details/{challenge.Id}");
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Челлендж одобрен и опубликован.";
            return RedirectToAction(nameof(Challenges), null, null, "admin-challenges");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectChallenge(int id)
        {
            if (!await IsAdminAuthenticatedAsync())
                return RedirectToAction(nameof(Login));

            var challenge = await _context.Challenges.FirstOrDefaultAsync(x => x.Id == id);
            if (challenge == null)
                return View("~/Views/Shared/NotFound.cshtml", "Челлендж не найден.");

            challenge.Status = ChallengeStatuses.Rejected;
            challenge.ReviewedAt = DateTime.UtcNow;
            await _notificationService.AddAsync(
                challenge.CreatedByUserId,
                NotificationTypes.Challenge,
                "Челлендж отклонен",
                $"Челлендж «{challenge.Title}» не прошел модерацию.",
                "/Challenges");
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Челлендж отклонен.";
            return RedirectToAction(nameof(Challenges), null, null, "admin-challenges");
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
    }
}
