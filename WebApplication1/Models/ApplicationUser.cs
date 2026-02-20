using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    public string SteamId { get; set; }
    public string PersonaName { get; set; }
    public string AvatarUrl { get; set; }
}