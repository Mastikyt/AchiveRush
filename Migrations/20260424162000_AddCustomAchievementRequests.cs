using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    [Migration("20260424162000_AddCustomAchievementRequests")]
    public partial class AddCustomAchievementRequests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CustomAchievementRequests]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [CustomAchievementRequests] (
                        [Id] int NOT NULL IDENTITY,
                        [GameId] int NOT NULL,
                        [RequestedByUserId] int NULL,
                        [Title] nvarchar(256) NOT NULL,
                        [Description] nvarchar(max) NOT NULL,
                        [ObtainMethod] nvarchar(2000) NOT NULL,
                        [IconUrl] nvarchar(2048) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [Status] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK_CustomAchievementRequests] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CustomAchievementRequests_Games_GameId] FOREIGN KEY ([GameId]) REFERENCES [Games] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_CustomAchievementRequests_Users_RequestedByUserId] FOREIGN KEY ([RequestedByUserId]) REFERENCES [Users] ([Id])
                    );
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomAchievementRequests_GameId' AND object_id = OBJECT_ID(N'[CustomAchievementRequests]'))
                    CREATE INDEX [IX_CustomAchievementRequests_GameId] ON [CustomAchievementRequests] ([GameId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomAchievementRequests_RequestedByUserId' AND object_id = OBJECT_ID(N'[CustomAchievementRequests]'))
                    CREATE INDEX [IX_CustomAchievementRequests_RequestedByUserId] ON [CustomAchievementRequests] ([RequestedByUserId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CustomAchievementRequests_Status_CreatedAt' AND object_id = OBJECT_ID(N'[CustomAchievementRequests]'))
                    CREATE INDEX [IX_CustomAchievementRequests_Status_CreatedAt] ON [CustomAchievementRequests] ([Status], [CreatedAt]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CustomAchievementRequests]', N'U') IS NOT NULL
                    DROP TABLE [CustomAchievementRequests];
                """);
        }
    }
}
