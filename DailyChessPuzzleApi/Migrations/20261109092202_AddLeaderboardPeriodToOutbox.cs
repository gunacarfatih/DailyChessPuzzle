using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChessPuzzleApi.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardPeriodToOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "LeaderboardCacheOutbox",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Period",
                table: "LeaderboardCacheOutbox",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PeriodKey",
                table: "LeaderboardCacheOutbox",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "LeaderboardCacheOutbox");

            migrationBuilder.DropColumn(
                name: "Period",
                table: "LeaderboardCacheOutbox");

            migrationBuilder.DropColumn(
                name: "PeriodKey",
                table: "LeaderboardCacheOutbox");
        }
    }
}
