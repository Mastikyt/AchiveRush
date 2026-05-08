using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    public partial class AddProofUrlToCustomAchievementClaimRequests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CustomAchievementClaimRequests]', N'U') IS NOT NULL
                    AND COL_LENGTH('dbo.CustomAchievementClaimRequests', 'ProofUrl') IS NULL
                    ALTER TABLE [CustomAchievementClaimRequests] ADD [ProofUrl] nvarchar(2048) NOT NULL CONSTRAINT [DF_CustomAchievementClaimRequests_ProofUrl] DEFAULT(N'') WITH VALUES;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CustomAchievementClaimRequests]', N'U') IS NOT NULL
                    AND EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_CustomAchievementClaimRequests_ProofUrl')
                    ALTER TABLE [CustomAchievementClaimRequests] DROP CONSTRAINT [DF_CustomAchievementClaimRequests_ProofUrl];

                IF OBJECT_ID(N'[CustomAchievementClaimRequests]', N'U') IS NOT NULL
                    AND COL_LENGTH('dbo.CustomAchievementClaimRequests', 'ProofUrl') IS NOT NULL
                    ALTER TABLE [CustomAchievementClaimRequests] DROP COLUMN [ProofUrl];
                """);
        }
    }
}
