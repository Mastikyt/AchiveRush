using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Text.Json;

namespace WebApplication1.Services
{
    public class CacheService
    {
        private readonly IDatabase _redis;
        private readonly SteamService _steam;

        // 🔥 fallback cache (in-memory)
        private static Dictionary<int, (DateTime time, Dictionary<string, double> data)> _memoryCache = new();

        public CacheService(IConnectionMultiplexer redis, SteamService steam)
        {
            _redis = redis.GetDatabase();
            _steam = steam;
        }

        public async Task<Dictionary<string, double>> GetOrCreateGlobalRates(int appId)
        {
            var key = $"global_rates:{appId}";

            // 🔥 1. ПЫТАЕМСЯ REDIS
            try
            {
                var cached = await _redis.StringGetAsync(key);
                if (!cached.IsNullOrEmpty)
                {
                    return JsonSerializer.Deserialize<Dictionary<string, double>>(cached)!;
                }
            }
            catch
            {
                // Redis умер — идем дальше
            }

            // 🔥 2. FALLBACK В ПАМЯТЬ
            if (_memoryCache.TryGetValue(appId, out var entry))
            {
                if ((DateTime.UtcNow - entry.time).TotalHours < 24)
                    return entry.data;
            }

            // 🔥 3. ДЁРГАЕМ STEAM (ПОСЛЕДНИЙ ВАРИАНТ)
            var data = await _steam.GetGlobalRates(appId);

            // 🔥 4. СОХРАНЯЕМ В REDIS
            try
            {
                await _redis.StringSetAsync(
                    key,
                    JsonSerializer.Serialize(data),
                    TimeSpan.FromHours(24)
                );
            }
            catch
            {
                // Redis может быть недоступен — игнорим
            }

            // 🔥 5. СОХРАНЯЕМ В ПАМЯТЬ
            _memoryCache[appId] = (DateTime.UtcNow, data);

            return data;
        }
    }
}
