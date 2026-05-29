using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    public class ChallengesController : Controller
    {
        private const long MaxCoverImageBytes = 4 * 1024 * 1024;
        private static readonly HashSet<string> AllowedCoverExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".gif"
        };

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly QuestProgressService _questProgressService;
        private readonly IWebHostEnvironment _environment;

        public ChallengesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            QuestProgressService questProgressService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _questProgressService = questProgressService;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? q, string? category, string? type, string? difficulty)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            var publicUser = await GetPublicUserAsync(identityUser);

            var searchQuery = SteamService.CleanText(q);
            var selectedCategory = NormalizeCategory(category, allowEmpty: true);
            var selectedType = NormalizeChallengeType(type, allowEmpty: true);
            var selectedDifficulty = NormalizeDifficulty(difficulty, allowEmpty: true);

            var query = _context.Challenges
                .AsNoTracking()
                .Include(c => c.Game)
                .Include(c => c.CreatedByUser)
                .Include(c => c.Participants)
                .Include(c => c.Submissions)
                    .ThenInclude(s => s.User)
                .Where(c => c.Status == ChallengeStatuses.Approved);

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(c =>
                    c.Title.Contains(searchQuery) ||
                    c.Description.Contains(searchQuery) ||
                    c.Category.Contains(searchQuery) ||
                    c.ChallengeType.Contains(searchQuery) ||
                    (c.Game != null && c.Game.Name.Contains(searchQuery)));
            }

            if (!string.IsNullOrWhiteSpace(selectedCategory))
                query = query.Where(c => c.Category == selectedCategory);

            if (!string.IsNullOrWhiteSpace(selectedType))
                query = query.Where(c => c.ChallengeType == selectedType);

            if (!string.IsNullOrWhiteSpace(selectedDifficulty))
                query = query.Where(c => c.Difficulty == selectedDifficulty);

            var challenges = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var model = new ChallengesIndexViewModel
            {
                IsSignedIn = publicUser != null,
                SearchQuery = searchQuery,
                SelectedCategory = selectedCategory,
                SelectedType = selectedType,
                SelectedDifficulty = selectedDifficulty,
                Challenges = challenges.Select(c => ToChallengeViewModel(c, publicUser)).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var identityUser = await _userManager.GetUserAsync(User);
            var publicUser = await GetPublicUserAsync(identityUser);

            return View(new ChallengeCreateViewModel
            {
                IsSignedIn = publicUser != null,
                Games = await GetGameOptionsAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxCoverImageBytes + 1024 * 1024)]
        public async Task<IActionResult> Create(ChallengeCreateInputModel input)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            var publicUser = await GetPublicUserAsync(identityUser);
            if (publicUser == null)
                return RedirectToAction("Login", "Account");

            var title = SteamService.CleanText(input.Title);
            var description = SteamService.CleanText(input.Description);
            var manualProofDescription = SteamService.CleanText(input.ManualProofDescription);
            var difficulty = NormalizeDifficulty(input.Difficulty);
            var category = NormalizeCategory(input.Category);
            var challengeType = NormalizeChallengeType(input.ChallengeType);
            var verificationType = input.VerificationType == ChallengeVerificationTypes.Manual
                ? ChallengeVerificationTypes.Manual
                : ChallengeVerificationTypes.Automatic;
            var autoGoalType = ResolveAutoGoalType(challengeType, input.AutoGoalType);
            var targetValue = autoGoalType == ChallengeAutoGoalTypes.GameCompletion100
                ? 1
                : Math.Clamp(input.TargetValue, 1, 500);

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["ChallengesError"] = "Введите название челленджа.";
                return RedirectToAction(nameof(Create));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                TempData["ChallengesError"] = "Введите описание челленджа.";
                return RedirectToAction(nameof(Create));
            }

            if (title.Length > 180 || description.Length > 2000)
            {
                TempData["ChallengesError"] = "Название или описание слишком длинные.";
                return RedirectToAction(nameof(Create));
            }

            int? gameId = null;
            if (input.GameId.HasValue)
            {
                var gameExists = await _context.Games.AnyAsync(g => g.Id == input.GameId.Value);
                if (gameExists)
                    gameId = input.GameId.Value;
            }

            if (verificationType == ChallengeVerificationTypes.Automatic && !gameId.HasValue)
            {
                TempData["ChallengesError"] = "Для автоматического челленджа выберите игру.";
                return RedirectToAction(nameof(Create));
            }

            if (verificationType == ChallengeVerificationTypes.Manual && string.IsNullOrWhiteSpace(manualProofDescription))
            {
                manualProofDescription = "Участник отправляет ссылку на скриншот, видео или профиль, а создатель челленджа подтверждает результат.";
            }

            var coverImageUrl = await SaveCoverImageAsync(input.CoverImage);
            if (coverImageUrl == null)
                return RedirectToAction(nameof(Create));

            if (string.IsNullOrWhiteSpace(coverImageUrl))
                coverImageUrl = NormalizeCoverImageUrl(input.CoverImageUrl);

            _context.Challenges.Add(new Challenge
            {
                Title = title,
                Description = description,
                Difficulty = difficulty,
                Category = category,
                ChallengeType = challengeType,
                VerificationType = verificationType,
                AutoGoalType = autoGoalType,
                TargetValue = targetValue,
                ManualProofDescription = manualProofDescription,
                CoverImageUrl = coverImageUrl,
                RewardExperience = Math.Clamp(input.RewardExperience, 1, 100000),
                ParticipantLimit = Math.Clamp(input.ParticipantLimit, 1, 1000),
                GameId = gameId,
                CreatedByUserId = publicUser.Id,
                CreatedAt = DateTime.UtcNow,
                Status = ChallengeStatuses.Pending
            });

            await _context.SaveChangesAsync();

            TempData["ChallengesSuccess"] = "Челлендж отправлен администратору на подтверждение.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            var publicUser = await GetPublicUserAsync(identityUser);

            var challenge = await _context.Challenges
                .AsNoTracking()
                .Include(c => c.Game)
                .Include(c => c.CreatedByUser)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Include(c => c.Submissions)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == ChallengeStatuses.Approved);

            if (challenge == null)
                return View("~/Views/Shared/NotFound.cshtml", "Челлендж не найден.");

            return View(new ChallengeDetailsViewModel
            {
                IsSignedIn = publicUser != null,
                Challenge = ToChallengeViewModel(challenge, publicUser),
                Participants = challenge.Participants
                    .OrderByDescending(p => p.Status == ChallengeParticipantStatuses.Completed)
                    .ThenBy(p => p.CompletedAt ?? DateTime.MaxValue)
                    .ThenBy(p => p.JoinedAt)
                    .Select(p => new ChallengeParticipantViewModel
                    {
                        UserName = p.User.SteamName,
                        SteamId = p.User.SteamId,
                        AvatarUrl = p.User.AvatarID,
                        JoinedAt = p.JoinedAt,
                        CompletedAt = p.CompletedAt,
                        Status = p.Status,
                        TimeSpent = p.CompletedAt.HasValue ? p.CompletedAt.Value - p.JoinedAt : null
                    })
                    .ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(int id, string? returnUrl)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            var publicUser = await GetPublicUserAsync(identityUser);
            if (publicUser == null)
                return RedirectToAction("Login", "Account");

            var challenge = await _context.Challenges
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == id && c.Status == ChallengeStatuses.Approved);

            if (challenge == null)
                return View("~/Views/Shared/NotFound.cshtml", "Челлендж не найден.");

            if (challenge.Participants.Any(p => p.UserId == publicUser.Id))
            {
                TempData["ChallengesError"] = "Ты уже участвуешь в этом челлендже.";
                return RedirectToLocalOrIndex(returnUrl);
            }

            if (challenge.Participants.Count >= challenge.ParticipantLimit)
            {
                TempData["ChallengesError"] = "В челлендже уже нет свободных мест.";
                return RedirectToLocalOrIndex(returnUrl);
            }

            _context.ChallengeParticipants.Add(new ChallengeParticipant
            {
                ChallengeId = challenge.Id,
                UserId = publicUser.Id,
                JoinedAt = DateTime.UtcNow,
                Status = ChallengeParticipantStatuses.Joined
            });

            await _context.SaveChangesAsync();
            TempData["ChallengesSuccess"] = "Ты присоединился к челленджу.";
            return RedirectToLocalOrIndex(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Check(int id, string? returnUrl)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            var publicUser = await GetPublicUserAsync(identityUser);
            if (publicUser == null)
                return RedirectToAction("Login", "Account");

            TempData["ChallengesSuccess"] = await _questProgressService.CheckAutomaticChallengeAsync(publicUser.Id, id);
            return RedirectToLocalOrIndex(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitProof(int id, string proofUrl, string? comment, string? returnUrl)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            var publicUser = await GetPublicUserAsync(identityUser);
            if (publicUser == null)
                return RedirectToAction("Login", "Account");

            var challenge = await _context.Challenges
                .FirstOrDefaultAsync(c => c.Id == id &&
                                          c.Status == ChallengeStatuses.Approved &&
                                          c.VerificationType == ChallengeVerificationTypes.Manual);

            if (challenge == null)
                return View("~/Views/Shared/NotFound.cshtml", "Ручной челлендж не найден.");

            var participant = await _context.ChallengeParticipants
                .FirstOrDefaultAsync(p => p.ChallengeId == id && p.UserId == publicUser.Id);

            if (participant == null)
            {
                TempData["ChallengesError"] = "Сначала нужно участвовать в челлендже.";
                return RedirectToLocalOrIndex(returnUrl);
            }

            if (participant.Status == ChallengeParticipantStatuses.Completed)
            {
                TempData["ChallengesSuccess"] = "Этот челлендж уже выполнен.";
                return RedirectToLocalOrIndex(returnUrl);
            }

            var cleanedProofUrl = (proofUrl ?? "").Trim();
            if (!Uri.TryCreate(cleanedProofUrl, UriKind.Absolute, out var proofUri) ||
                proofUri.Scheme is not ("http" or "https"))
            {
                TempData["ChallengesError"] = "Ссылка на доказательство должна начинаться с http:// или https://.";
                return RedirectToLocalOrIndex(returnUrl);
            }

            var hasPending = await _context.ChallengeSubmissions.AnyAsync(s =>
                s.ChallengeId == id &&
                s.UserId == publicUser.Id &&
                s.Status == ChallengeSubmissionStatuses.Pending);

            if (hasPending)
            {
                TempData["ChallengesError"] = "Доказательство уже ожидает проверки.";
                return RedirectToLocalOrIndex(returnUrl);
            }

            _context.ChallengeSubmissions.Add(new ChallengeSubmission
            {
                ChallengeId = id,
                UserId = publicUser.Id,
                ProofUrl = cleanedProofUrl,
                Comment = SteamService.CleanText(comment),
                CreatedAt = DateTime.UtcNow,
                Status = ChallengeSubmissionStatuses.Pending
            });

            await _context.SaveChangesAsync();
            TempData["ChallengesSuccess"] = "Доказательство отправлено создателю челленджа.";
            return RedirectToLocalOrIndex(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveSubmission(int id, string? returnUrl)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            var publicUser = await GetPublicUserAsync(identityUser);
            if (publicUser == null)
                return RedirectToAction("Login", "Account");

            TempData["ChallengesSuccess"] = await _questProgressService.ApproveManualSubmissionAsync(id, publicUser.Id);
            return RedirectToLocalOrIndex(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectSubmission(int id, string? returnUrl)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            var publicUser = await GetPublicUserAsync(identityUser);
            if (publicUser == null)
                return RedirectToAction("Login", "Account");

            TempData["ChallengesSuccess"] = await _questProgressService.RejectManualSubmissionAsync(id, publicUser.Id);
            return RedirectToLocalOrIndex(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefreshDaily(string? returnUrl)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            var publicUser = await GetPublicUserAsync(identityUser);
            if (publicUser == null)
                return RedirectToAction("Login", "Account");

            var assignment = await _questProgressService.EvaluateDailyQuestAsync(publicUser.Id);
            TempData["ChallengesSuccess"] = assignment?.Completed == true
                ? "Ежедневный квест выполнен, опыт начислен."
                : "Прогресс ежедневного квеста обновлен.";

            return RedirectToLocalOrIndex(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RerollDaily(string? returnUrl)
        {
            var identityUser = await _userManager.GetUserAsync(User);
            var publicUser = await GetPublicUserAsync(identityUser);
            if (publicUser == null)
                return RedirectToAction("Login", "Account");

            TempData["ChallengesSuccess"] = await _questProgressService.RerollDailyQuestAsync(publicUser.Id);
            return RedirectToLocalOrIndex(returnUrl);
        }

        private async Task<List<GameOptionViewModel>> GetGameOptionsAsync()
        {
            return await _context.Games
                .AsNoTracking()
                .OrderBy(g => g.Name)
                .Select(g => new GameOptionViewModel
                {
                    Id = g.Id,
                    Name = g.Name
                })
                .ToListAsync();
        }

        private async Task<User?> GetPublicUserAsync(ApplicationUser? identityUser)
        {
            if (identityUser == null || string.IsNullOrWhiteSpace(identityUser.SteamId))
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.SteamId == identityUser.SteamId);
        }

        private IActionResult RedirectToLocalOrIndex(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        private async Task<string?> SaveCoverImageAsync(IFormFile? image)
        {
            if (image == null || image.Length == 0)
                return "";

            if (image.Length > MaxCoverImageBytes)
            {
                TempData["ChallengesError"] = "Фото челленджа должно быть меньше 4 МБ.";
                return null;
            }

            var extension = Path.GetExtension(image.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedCoverExtensions.Contains(extension))
            {
                TempData["ChallengesError"] = "Загрузи фото в формате JPG, PNG, WEBP или GIF.";
                return null;
            }

            var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "challenges");
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            await using var stream = System.IO.File.Create(fullPath);
            await image.CopyToAsync(stream);

            return $"/uploads/challenges/{fileName}";
        }

        private static string NormalizeCoverImageUrl(string? imageUrl)
        {
            var cleaned = (imageUrl ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
                return "";

            if (cleaned.StartsWith("/", StringComparison.Ordinal))
                return cleaned.Length <= 2048 ? cleaned : "";

            return Uri.TryCreate(cleaned, UriKind.Absolute, out var uri) &&
                   uri.Scheme is "http" or "https" &&
                   cleaned.Length <= 2048
                ? cleaned
                : "";
        }

        private static string NormalizeDifficulty(string? difficulty, bool allowEmpty = false)
        {
            if (allowEmpty && string.IsNullOrWhiteSpace(difficulty))
                return "";

            return ChallengeDifficulties.All.Contains(difficulty)
                ? difficulty!
                : ChallengeDifficulties.Normal;
        }

        private static string NormalizeCategory(string? category, bool allowEmpty = false)
        {
            if (allowEmpty && string.IsNullOrWhiteSpace(category))
                return "";

            return ChallengeCategories.All.Contains(category)
                ? category!
                : ChallengeCategories.Completion;
        }

        private static string NormalizeChallengeType(string? challengeType, bool allowEmpty = false)
        {
            if (allowEmpty && string.IsNullOrWhiteSpace(challengeType))
                return "";

            return ChallengeTypes.All.Contains(challengeType)
                ? challengeType!
                : ChallengeTypes.Completion;
        }

        private static string ResolveAutoGoalType(string challengeType, string? requestedAutoGoalType)
        {
            if (ChallengeAutoGoalTypes.All.Contains(requestedAutoGoalType))
                return requestedAutoGoalType!;

            return challengeType switch
            {
                ChallengeTypes.AchievementHunt => ChallengeAutoGoalTypes.EarnAchievementsInGame,
                ChallengeTypes.RareAchievementHunt => ChallengeAutoGoalTypes.EarnRareAchievementsInGame,
                _ => ChallengeAutoGoalTypes.GameCompletion100
            };
        }

        private static ChallengeListItemViewModel ToChallengeViewModel(Challenge challenge, User? publicUser)
        {
            var participant = publicUser == null
                ? null
                : challenge.Participants.FirstOrDefault(p => p.UserId == publicUser.Id);

            var pendingForUser = publicUser != null && challenge.Submissions.Any(s =>
                s.UserId == publicUser.Id &&
                s.Status == ChallengeSubmissionStatuses.Pending);

            var isCreator = publicUser != null && challenge.CreatedByUserId == publicUser.Id;

            return new ChallengeListItemViewModel
            {
                Id = challenge.Id,
                Title = challenge.Title,
                Description = challenge.Description,
                Difficulty = challenge.Difficulty,
                Category = challenge.Category,
                ChallengeType = challenge.ChallengeType,
                VerificationType = challenge.VerificationType,
                AutoGoalType = challenge.AutoGoalType,
                TargetValue = challenge.TargetValue,
                ManualProofDescription = challenge.ManualProofDescription,
                CoverImageUrl = string.IsNullOrWhiteSpace(challenge.CoverImageUrl)
                    ? challenge.Game?.AvatarUrl ?? ""
                    : challenge.CoverImageUrl,
                RewardExperience = challenge.RewardExperience,
                ParticipantLimit = challenge.ParticipantLimit,
                ParticipantsCount = challenge.Participants.Count,
                CompletedCount = challenge.Participants.Count(p => p.Status == ChallengeParticipantStatuses.Completed),
                GameName = challenge.Game?.Name ?? "",
                GameAvatarUrl = challenge.Game?.AvatarUrl ?? "",
                CreatedByName = challenge.CreatedByUser?.SteamName ?? "Система",
                UserParticipantStatus = participant?.Status,
                UserCanJoin = publicUser != null &&
                              participant == null &&
                              challenge.Participants.Count < challenge.ParticipantLimit,
                UserCanSubmitProof = publicUser != null &&
                                     challenge.VerificationType == ChallengeVerificationTypes.Manual &&
                                     participant?.Status == ChallengeParticipantStatuses.Joined &&
                                     !pendingForUser,
                UserHasPendingSubmission = pendingForUser,
                UserCompleted = participant?.Status == ChallengeParticipantStatuses.Completed,
                IsCreator = isCreator,
                PendingSubmissions = isCreator
                    ? challenge.Submissions
                        .Where(s => s.Status == ChallengeSubmissionStatuses.Pending)
                        .OrderBy(s => s.CreatedAt)
                        .Select(s => new ChallengeSubmissionReviewItemViewModel
                        {
                            Id = s.Id,
                            UserName = s.User.SteamName,
                            ProofUrl = s.ProofUrl,
                            Comment = s.Comment,
                            CreatedAt = s.CreatedAt
                        })
                        .ToList()
                    : new List<ChallengeSubmissionReviewItemViewModel>()
            };
        }
    }
}
