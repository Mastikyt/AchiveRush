using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengeDisplayFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Challenges",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Completion");

            migrationBuilder.AddColumn<string>(
                name: "ChallengeType",
                table: "Challenges",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Completion");

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Challenges",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TargetValue",
                table: "Challenges",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_Status_Category_ChallengeType",
                table: "Challenges",
                columns: new[] { "Status", "Category", "ChallengeType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Challenges_Status_Category_ChallengeType",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "ChallengeType",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "TargetValue",
                table: "Challenges");
        }
    }
}
