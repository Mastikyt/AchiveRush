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

    public DbSet<CustomAchievementRequest> CustomAchievementRequests { get; set; }

    public DbSet<CustomAchievementClaimRequest> CustomAchievementClaimRequests { get; set; }

    public DbSet<Challenge> Challenges { get; set; }

    public DbSet<ChallengeParticipant> ChallengeParticipants { get; set; }

    public DbSet<ChallengeSubmission> ChallengeSubmissions { get; set; }

    public DbSet<DailyQuest> DailyQuests { get; set; }

    public DbSet<DailyQuestAssignment> DailyQuestAssignments { get; set; }

    public DbSet<DailyQuestStat> DailyQuestStats { get; set; }

    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<User>()
            .Property(u => u.SteamId)
            .HasMaxLength(64);

        builder.Entity<Achievement>()
            .Property(a => a.ApiName)
            .HasMaxLength(256);

        builder.Entity<Achievement>()
            .Property(a => a.IconUrl)
            .HasMaxLength(2048);

        builder.Entity<Achievement>()
            .Property(a => a.ObtainMethod)
            .HasMaxLength(2000);

        builder.Entity<User>()
            .HasIndex(u => u.SteamId);

        builder.Entity<User>()
            .HasIndex(u => u.TotalAchievements);

        builder.Entity<Game>()
            .HasIndex(g => g.SteamAppId);

        builder.Entity<Achievement>()
            .HasIndex(a => new { a.GameId, a.ApiName });

        builder.Entity<Achievement>()
            .HasIndex(a => a.CreatedByUserId);

        builder.Entity<CustomAchievementRequest>()
            .Property(r => r.Status)
            .HasMaxLength(32);

        builder.Entity<CustomAchievementRequest>()
            .Property(r => r.Title)
            .HasMaxLength(256);

        builder.Entity<CustomAchievementRequest>()
            .Property(r => r.IconUrl)
            .HasMaxLength(2048);

        builder.Entity<CustomAchievementRequest>()
            .Property(r => r.ObtainMethod)
            .HasMaxLength(2000);

        builder.Entity<CustomAchievementRequest>()
            .HasIndex(r => new { r.Status, r.CreatedAt });

        builder.Entity<UserAchievement>()
            .HasIndex(ua => new { ua.UserId, ua.Completed, ua.UnlockTime });

        builder.Entity<UserAchievement>()
            .HasIndex(ua => new { ua.UserId, ua.AchievementId });

        builder.Entity<CustomAchievementClaimRequest>()
            .Property(r => r.Status)
            .HasMaxLength(32);

        builder.Entity<CustomAchievementClaimRequest>()
            .Property(r => r.Comment)
            .HasMaxLength(2000);

        builder.Entity<CustomAchievementClaimRequest>()
            .Property(r => r.ProofUrl)
            .HasMaxLength(2048);

        builder.Entity<CustomAchievementClaimRequest>()
            .HasIndex(r => new { r.Status, r.CreatedAt });

        builder.Entity<CustomAchievementClaimRequest>()
            .HasIndex(r => new { r.UserId, r.AchievementId, r.Status });

        builder.Entity<User>()
            .Property(u => u.QuestExperience)
            .HasDefaultValue(0);

        builder.Entity<User>()
            .Property(u => u.LastNotifiedLevel)
            .HasDefaultValue(0);

        builder.Entity<GameRequest>()
            .HasOne(r => r.RequestedByUser)
            .WithMany()
            .HasForeignKey(r => r.RequestedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Challenge>()
            .Property(c => c.Title)
            .HasMaxLength(180);

        builder.Entity<Challenge>()
            .Property(c => c.Description)
            .HasMaxLength(2000);

        builder.Entity<Challenge>()
            .Property(c => c.ManualProofDescription)
            .HasMaxLength(1000);

        builder.Entity<Challenge>()
            .Property(c => c.Status)
            .HasMaxLength(32);

        builder.Entity<Challenge>()
            .Property(c => c.Difficulty)
            .HasMaxLength(32);

        builder.Entity<Challenge>()
            .Property(c => c.Category)
            .HasMaxLength(64);

        builder.Entity<Challenge>()
            .Property(c => c.ChallengeType)
            .HasMaxLength(64);

        builder.Entity<Challenge>()
            .Property(c => c.VerificationType)
            .HasMaxLength(32);

        builder.Entity<Challenge>()
            .Property(c => c.AutoGoalType)
            .HasMaxLength(64);

        builder.Entity<Challenge>()
            .Property(c => c.CoverImageUrl)
            .HasMaxLength(2048);

        builder.Entity<Challenge>()
            .HasIndex(c => new { c.Status, c.CreatedAt });

        builder.Entity<Challenge>()
            .HasIndex(c => new { c.Status, c.Category, c.ChallengeType });

        builder.Entity<Challenge>()
            .HasIndex(c => new { c.VerificationType, c.GameId });

        builder.Entity<Challenge>()
            .HasOne(c => c.CreatedByUser)
            .WithMany(u => u.CreatedChallenges)
            .HasForeignKey(c => c.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Challenge>()
            .HasOne(c => c.Game)
            .WithMany()
            .HasForeignKey(c => c.GameId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ChallengeParticipant>()
            .Property(p => p.Status)
            .HasMaxLength(32);

        builder.Entity<ChallengeParticipant>()
            .HasIndex(p => new { p.ChallengeId, p.UserId })
            .IsUnique();

        builder.Entity<ChallengeParticipant>()
            .HasIndex(p => new { p.UserId, p.Status });

        builder.Entity<ChallengeParticipant>()
            .HasOne(p => p.Challenge)
            .WithMany(c => c.Participants)
            .HasForeignKey(p => p.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChallengeParticipant>()
            .HasOne(p => p.User)
            .WithMany(u => u.ChallengeParticipations)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChallengeSubmission>()
            .Property(s => s.Status)
            .HasMaxLength(32);

        builder.Entity<ChallengeSubmission>()
            .Property(s => s.ProofUrl)
            .HasMaxLength(2048);

        builder.Entity<ChallengeSubmission>()
            .Property(s => s.Comment)
            .HasMaxLength(2000);

        builder.Entity<ChallengeSubmission>()
            .HasIndex(s => new { s.ChallengeId, s.Status, s.CreatedAt });

        builder.Entity<ChallengeSubmission>()
            .HasIndex(s => new { s.UserId, s.Status });

        builder.Entity<ChallengeSubmission>()
            .HasOne(s => s.Challenge)
            .WithMany(c => c.Submissions)
            .HasForeignKey(s => s.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChallengeSubmission>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChallengeSubmission>()
            .HasOne(s => s.ReviewedByUser)
            .WithMany()
            .HasForeignKey(s => s.ReviewedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<DailyQuest>()
            .Property(q => q.Title)
            .HasMaxLength(180);

        builder.Entity<DailyQuest>()
            .Property(q => q.Description)
            .HasMaxLength(1000);

        builder.Entity<DailyQuest>()
            .Property(q => q.Difficulty)
            .HasMaxLength(32);

        builder.Entity<DailyQuest>()
            .Property(q => q.QuestType)
            .HasMaxLength(64);

        builder.Entity<DailyQuest>()
            .HasIndex(q => new { q.IsActive, q.Difficulty });

        builder.Entity<DailyQuestAssignment>()
            .HasIndex(a => new { a.UserId, a.AssignedDate })
            .IsUnique();

        builder.Entity<DailyQuestAssignment>()
            .HasOne(a => a.User)
            .WithMany(u => u.DailyQuestAssignments)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<DailyQuestAssignment>()
            .HasOne(a => a.DailyQuest)
            .WithMany(q => q.Assignments)
            .HasForeignKey(a => a.DailyQuestId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<DailyQuestStat>()
            .HasIndex(s => new { s.UserId, s.StatDate })
            .IsUnique();

        builder.Entity<DailyQuestStat>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Notification>()
            .Property(n => n.Type)
            .HasMaxLength(64);

        builder.Entity<Notification>()
            .Property(n => n.Title)
            .HasMaxLength(180);

        builder.Entity<Notification>()
            .Property(n => n.Message)
            .HasMaxLength(1000);

        builder.Entity<Notification>()
            .Property(n => n.Url)
            .HasMaxLength(2048);

        builder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.ReadAt, n.CreatedAt });

        builder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
