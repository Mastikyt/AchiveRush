using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomAchievementVoting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CustomAchievementRequests]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('dbo.CustomAchievementRequests', 'ResolvedAt') IS NULL
                        ALTER TABLE [CustomAchievementRequests] ADD [ResolvedAt] datetime2 NULL;

                    IF COL_LENGTH('dbo.CustomAchievementRequests', 'VotingEndsAt') IS NULL
                        ALTER TABLE [CustomAchievementRequests] ADD [VotingEndsAt] datetime2 NULL;

                    IF COL_LENGTH('dbo.CustomAchievementRequests', 'VotingStartedAt') IS NULL
                        ALTER TABLE [CustomAchievementRequests] ADD [VotingStartedAt] datetime2 NULL;

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomAchievementRequests_Status_VotingEndsAt' AND object_id = OBJECT_ID(N'[CustomAchievementRequests]'))
                        CREATE INDEX [IX_CustomAchievementRequests_Status_VotingEndsAt] ON [CustomAchievementRequests] ([Status], [VotingEndsAt]);
                END

                IF OBJECT_ID(N'[CustomAchievementVotes]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [CustomAchievementVotes] (
                        [Id] int NOT NULL IDENTITY,
                        [CustomAchievementRequestId] int NOT NULL,
                        [UserId] int NOT NULL,
                        [IsPositive] bit NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_CustomAchievementVotes] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CustomAchievementVotes_CustomAchievementRequests_CustomAchievementRequestId] FOREIGN KEY ([CustomAchievementRequestId]) REFERENCES [CustomAchievementRequests] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_CustomAchievementVotes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                    );
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomAchievementVotes_CustomAchievementRequestId_UserId' AND object_id = OBJECT_ID(N'[CustomAchievementVotes]'))
                    CREATE UNIQUE INDEX [IX_CustomAchievementVotes_CustomAchievementRequestId_UserId] ON [CustomAchievementVotes] ([CustomAchievementRequestId], [UserId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomAchievementVotes_UserId' AND object_id = OBJECT_ID(N'[CustomAchievementVotes]'))
                    CREATE INDEX [IX_CustomAchievementVotes_UserId] ON [CustomAchievementVotes] ([UserId]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CustomAchievementVotes]', N'U') IS NOT NULL
                    DROP TABLE [CustomAchievementVotes];

                IF OBJECT_ID(N'[CustomAchievementRequests]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomAchievementRequests_Status_VotingEndsAt' AND object_id = OBJECT_ID(N'[CustomAchievementRequests]'))
                        DROP INDEX [IX_CustomAchievementRequests_Status_VotingEndsAt] ON [CustomAchievementRequests];

                    IF COL_LENGTH('dbo.CustomAchievementRequests', 'ResolvedAt') IS NOT NULL
                        ALTER TABLE [CustomAchievementRequests] DROP COLUMN [ResolvedAt];

                    IF COL_LENGTH('dbo.CustomAchievementRequests', 'VotingEndsAt') IS NOT NULL
                        ALTER TABLE [CustomAchievementRequests] DROP COLUMN [VotingEndsAt];

                    IF COL_LENGTH('dbo.CustomAchievementRequests', 'VotingStartedAt') IS NOT NULL
                        ALTER TABLE [CustomAchievementRequests] DROP COLUMN [VotingStartedAt];
                END
                """);
        }
    }
}
