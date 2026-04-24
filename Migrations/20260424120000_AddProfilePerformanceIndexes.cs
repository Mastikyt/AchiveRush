using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    [Migration("20260424120000_AddProfilePerformanceIndexes")]
    public partial class AddProfilePerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SteamId",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ApiName",
                table: "Achievements",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SteamId",
                table: "Users",
                column: "SteamId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TotalAchievements",
                table: "Users",
                column: "TotalAchievements");

            migrationBuilder.CreateIndex(
                name: "IX_Games_SteamAppId",
                table: "Games",
                column: "SteamAppId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_GameId_ApiName",
                table: "Achievements",
                columns: new[] { "GameId", "ApiName" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_Completed_UnlockTime",
                table: "UserAchievements",
                columns: new[] { "UserId", "Completed", "UnlockTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_SteamId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TotalAchievements",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Games_SteamAppId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Achievements_GameId_ApiName",
                table: "Achievements");

            migrationBuilder.DropIndex(
                name: "IX_UserAchievements_UserId_Completed_UnlockTime",
                table: "UserAchievements");

            migrationBuilder.DropIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "UserAchievements");

            migrationBuilder.AlterColumn<string>(
                name: "SteamId",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "ApiName",
                table: "Achievements",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);
        }
    }
}
