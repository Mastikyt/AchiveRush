using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    public partial class AddCustomAchievementClaimRequests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
                END

                IF OBJECT_ID(N'[CustomAchievementClaimRequests]', N'U') IS NOT NULL
                    AND COL_LENGTH('dbo.CustomAchievementClaimRequests', 'ProofUrl') IS NULL
                    ALTER TABLE [CustomAchievementClaimRequests] ADD [ProofUrl] nvarchar(2048) NOT NULL CONSTRAINT [DF_CustomAchievementClaimRequests_ProofUrl] DEFAULT(N'') WITH VALUES;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomAchievementClaimRequests_AchievementId' AND object_id = OBJECT_ID(N'[CustomAchievementClaimRequests]'))
                    CREATE INDEX [IX_CustomAchievementClaimRequests_AchievementId] ON [CustomAchievementClaimRequests] ([AchievementId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomAchievementClaimRequests_Status_CreatedAt' AND object_id = OBJECT_ID(N'[CustomAchievementClaimRequests]'))
                    CREATE INDEX [IX_CustomAchievementClaimRequests_Status_CreatedAt] ON [CustomAchievementClaimRequests] ([Status], [CreatedAt]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomAchievementClaimRequests_UserId_AchievementId_Status' AND object_id = OBJECT_ID(N'[CustomAchievementClaimRequests]'))
                    CREATE INDEX [IX_CustomAchievementClaimRequests_UserId_AchievementId_Status] ON [CustomAchievementClaimRequests] ([UserId], [AchievementId], [Status]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Duplicate safety migration: the earlier migration owns the table rollback.
        }
    }
}
