using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Services
{
    public class GlobalRatesUpdater : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public GlobalRatesUpdater(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var steam = scope.ServiceProvider.GetRequiredService<SteamService>();

                var games = await db.Games.ToListAsync();

                foreach (var game in games)
                {
                    var rates = await steam.GetGlobalRates(game.SteamAppId);

                    var achievements = await db.Achievements
                        .Where(a => a.GameId == game.Id)
                        .ToListAsync();

                    foreach (var ach in achievements)
                    {
                        if (rates.TryGetValue(ach.ApiName, out var percent))
                            ach.GlobalUnlockRate = percent;
                    }
                }

                await db.SaveChangesAsync();

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
