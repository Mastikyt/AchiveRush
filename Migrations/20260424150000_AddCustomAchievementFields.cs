using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    [Migration("20260424150000_AddCustomAchievementFields")]
    public partial class AddCustomAchievementFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Achievements_Users_CreatedByUserId')
                    ALTER TABLE [Achievements] ADD CONSTRAINT [FK_Achievements_Users_CreatedByUserId]
                    FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Achievements_Users_CreatedByUserId')
                    ALTER TABLE [Achievements] DROP CONSTRAINT [FK_Achievements_Users_CreatedByUserId];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Achievements_CreatedByUserId' AND object_id = OBJECT_ID(N'[Achievements]'))
                    DROP INDEX [IX_Achievements_CreatedByUserId] ON [Achievements];

                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Achievements_IsCustom')
                    ALTER TABLE [Achievements] DROP CONSTRAINT [DF_Achievements_IsCustom];

                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Achievements_ObtainMethod')
                    ALTER TABLE [Achievements] DROP CONSTRAINT [DF_Achievements_ObtainMethod];

                IF COL_LENGTH('dbo.Achievements', 'CreatedAt') IS NOT NULL
                    ALTER TABLE [Achievements] DROP COLUMN [CreatedAt];

                IF COL_LENGTH('dbo.Achievements', 'CreatedByUserId') IS NOT NULL
                    ALTER TABLE [Achievements] DROP COLUMN [CreatedByUserId];

                IF COL_LENGTH('dbo.Achievements', 'IsCustom') IS NOT NULL
                    ALTER TABLE [Achievements] DROP COLUMN [IsCustom];

                IF COL_LENGTH('dbo.Achievements', 'ObtainMethod') IS NOT NULL
                    ALTER TABLE [Achievements] DROP COLUMN [ObtainMethod];

                IF COL_LENGTH('dbo.Achievements', 'IconUrl') IS NOT NULL
                    ALTER TABLE [Achievements] ALTER COLUMN [IconUrl] nvarchar(max) NULL;
                """);
        }
    }
}
