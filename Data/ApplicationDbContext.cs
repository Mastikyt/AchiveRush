using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Game> Games { get; set; }
    public DbSet<Achievement> Achievements { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<UserAchievement> UserAchievements { get; set; }
    public DbSet<UserScore> UserScores { get; set; }

    public DbSet<GameRequest> GameRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<User>()
            .Property(u => u.SteamId)
            .HasMaxLength(64);

        builder.Entity<Achievement>()
            .Property(a => a.ApiName)
            .HasMaxLength(256);

        builder.Entity<User>()
            .HasIndex(u => u.SteamId);

        builder.Entity<User>()
            .HasIndex(u => u.TotalAchievements);

        builder.Entity<Game>()
            .HasIndex(g => g.SteamAppId);

        builder.Entity<Achievement>()
            .HasIndex(a => new { a.GameId, a.ApiName });

        builder.Entity<UserAchievement>()
            .HasIndex(ua => new { ua.UserId, ua.Completed, ua.UnlockTime });

        builder.Entity<UserAchievement>()
            .HasIndex(ua => new { ua.UserId, ua.AchievementId });
    }
}
