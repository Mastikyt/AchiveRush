using System.Text.Json;

namespace WebApplication1.Services
{
    public class AdminAccessService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public AdminAccessService(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _configuration = configuration;
        }

        public async Task<bool> IsAllowedAsync(ApplicationUser? user)
        {
            if (user == null)
                return false;

            var adminUsers = await LoadAsync();

            var steamId = Normalize(user.SteamId);
            var userName = Normalize(user.UserName);
            var personaName = Normalize(user.PersonaName);

            return (adminUsers.SteamIds ?? new List<string>()).Select(Normalize).Any(x => x == steamId) ||
                   (adminUsers.UserNames ?? new List<string>()).Select(Normalize).Any(x => x == userName || x == personaName);
        }

        public string GetConfiguredFileName()
        {
            return _configuration["AdminSettings:AllowedUsersFile"] ?? "AdminUsers.json";
        }

        private async Task<AdminUsersFile> LoadAsync()
        {
            var configuredPath = GetConfiguredFileName();
            var path = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(_environment.ContentRootPath, configuredPath);

            if (!File.Exists(path))
                return new AdminUsersFile();

            try
            {
                await using var stream = File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<AdminUsersFile>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new AdminUsersFile();
            }
            catch (JsonException)
            {
                return new AdminUsersFile();
            }
        }

        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? ""
                : value.Trim().ToLowerInvariant();
        }

        private sealed class AdminUsersFile
        {
            public List<string> SteamIds { get; set; } = new();

            public List<string> UserNames { get; set; } = new();
        }
    }
}
