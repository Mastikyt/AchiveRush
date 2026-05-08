using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Infrastructure
{
    public static class DatabaseSchemaGuard
    {
        public static async Task EnsureAsync(ApplicationDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[Achievements]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('dbo.Achievements', 'IconUrl') IS NULL
                        ALTER TABLE [Achievements] ADD [IconUrl] nvarchar(2048) NULL;
                    ELSE
                        ALTER TABLE [Achievements] ALTER COLUMN [IconUrl] nvarchar(2048) NULL;

                    IF COL_LENGTH('dbo.Achievements', 'CreatedAt') IS NULL
                        ALTER TABLE [Achievements] ADD [CreatedAt] datetime2 NULL;

                    IF COL_LENGTH('dbo.Achievements', 'CreatedByUserId') IS NULL
                        ALTER TABLE [Achievements] ADD [CreatedByUserId] int NULL;

                    IF COL_LENGTH('dbo.Achievements', 'IsCustom') IS NULL
                        ALTER TABLE [Achievements] ADD [IsCustom] bit NOT NULL CONSTRAINT [DF_Achievements_IsCustom] DEFAULT(0) WITH VALUES;

                    IF COL_LENGTH('dbo.Achievements', 'ObtainMethod') IS NULL
                        ALTER TABLE [Achievements] ADD [ObtainMethod] nvarchar(2000) NOT NULL CONSTRAINT [DF_Achievements_ObtainMethod] DEFAULT(N'') WITH VALUES;

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Achievements_CreatedByUserId' AND object_id = OBJECT_ID(N'[Achievements]'))
                        CREATE INDEX [IX_Achievements_CreatedByUserId] ON [Achievements] ([CreatedByUserId]);

                    IF OBJECT_ID(N'[Users]', N'U') IS NOT NULL
                        AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Achievements_Users_CreatedByUserId')
                        ALTER TABLE [Achievements] ADD CONSTRAINT [FK_Achievements_Users_CreatedByUserId]
                        FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]);
                END

                IF OBJECT_ID(N'[CustomAchievementClaimRequests]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [CustomAchievementClaimRequests] (
                        [Id] int NOT NULL IDENTITY,
                        [AchievementId] int NOT NULL,
                        [UserId] int NOT NULL,
                        [Comment] nvarchar(2000) NOT NULL DEFAULT(N''),
                        [ProofUrl] nvarchar(2048) NOT NULL DEFAULT(N''),
                        [CreatedAt] datetime2 NOT NULL,
                        [Status] nvarchar(32) NOT NULL DEFAULT(N'Pending'),
                        CONSTRAINT [PK_CustomAchievementClaimRequests] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CustomAchievementClaimRequests_Achievements_AchievementId] FOREIGN KEY ([AchievementId]) REFERENCES [Achievements] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_CustomAchievementClaimRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                    );

                    CREATE INDEX [IX_CustomAchievementClaimRequests_AchievementId] ON [CustomAchievementClaimRequests] ([AchievementId]);
                    CREATE INDEX [IX_CustomAchievementClaimRequests_Status_CreatedAt] ON [CustomAchievementClaimRequests] ([Status], [CreatedAt]);
                    CREATE INDEX [IX_CustomAchievementClaimRequests_UserId_AchievementId_Status] ON [CustomAchievementClaimRequests] ([UserId], [AchievementId], [Status]);
                END

                IF OBJECT_ID(N'[CustomAchievementClaimRequests]', N'U') IS NOT NULL
                    AND COL_LENGTH('dbo.CustomAchievementClaimRequests', 'ProofUrl') IS NULL
                    ALTER TABLE [CustomAchievementClaimRequests] ADD [ProofUrl] nvarchar(2048) NOT NULL CONSTRAINT [DF_CustomAchievementClaimRequests_ProofUrl] DEFAULT(N'') WITH VALUES;
                """);
        }
    }
}
