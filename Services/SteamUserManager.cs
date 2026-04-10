using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Services
{


    public class SteamUserManager
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public SteamUserManager(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<ApplicationUser> CreateOrUpdateAsync(Player profile)
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(x => x.SteamId == profile.Steamid);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = profile.Steamid,
                    SteamId = profile.Steamid,
                    PersonaName = profile.Personaname,
                    AvatarUrl = profile.Avatarfull
                };

                await _userManager.CreateAsync(user);
            }
            else
            {
                user.PersonaName = profile.Personaname;
                user.AvatarUrl = profile.Avatarfull;
                await _userManager.UpdateAsync(user);
            }

            return user;
        }
    }
}