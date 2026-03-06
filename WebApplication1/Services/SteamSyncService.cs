using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

public class SteamSyncService
{
    private readonly ApplicationDbContext _db;
    private readonly SteamService _steam;

    public SteamSyncService(ApplicationDbContext db, SteamService steam)
    {
        _db = db;
        _steam = steam;
    }

    public async Task SyncUser(string steamId)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.SteamId == steamId);

        if (user == null)
        {
            var profile = await _steam.GetProfileAsync(steamId);

            user = new User
            {
                SteamId = steamId,
                SteamName = profile.Personaname,
                AvatarID = profile.Avatarfull
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        user.LastSync = DateTime.Now;

        await _db.SaveChangesAsync();
    }
}