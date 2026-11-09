using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DailyChessPuzzleApi.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaderboardScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Period = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    SolvedCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardScores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardScores_ClientId_Period_PeriodKey",
                table: "LeaderboardScores",
                columns: new[] { "ClientId", "Period", "PeriodKey" },
                unique: true,
                filter: "\"CountryCode\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardScores_ClientId_Period_PeriodKey_CountryCode",
                table: "LeaderboardScores",
                columns: new[] { "ClientId", "Period", "PeriodKey", "CountryCode" },
                unique: true,
                filter: "\"CountryCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardScores_Period_PeriodKey_CountryCode_TotalScore_S~",
                table: "LeaderboardScores",
                columns: new[] { "Period", "PeriodKey", "CountryCode", "TotalScore", "SolvedCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaderboardScores");
        }
    }
}
