using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DailyChessPuzzleApi.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardCacheOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaderboardCacheOutbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CacheKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    SolvedCount = table.Column<int>(type: "integer", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardCacheOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardCacheOutbox_IsProcessed_CreatedAtUtc",
                table: "LeaderboardCacheOutbox",
                columns: new[] { "IsProcessed", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaderboardCacheOutbox");
        }
    }
}
