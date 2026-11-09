using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChessPuzzleApi.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PuzzleAttempts_CountryCode_PlayedOn",
                table: "PuzzleAttempts",
                columns: new[] { "CountryCode", "PlayedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleAttempts_PlayedOn",
                table: "PuzzleAttempts",
                column: "PlayedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PuzzleAttempts_CountryCode_PlayedOn",
                table: "PuzzleAttempts");

            migrationBuilder.DropIndex(
                name: "IX_PuzzleAttempts_PlayedOn",
                table: "PuzzleAttempts");
        }
    }
}
