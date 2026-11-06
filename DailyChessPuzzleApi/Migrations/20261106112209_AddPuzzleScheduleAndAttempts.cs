using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DailyChessPuzzleApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPuzzleScheduleAndAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Puzzles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PuzzleDate",
                table: "Puzzles",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateTable(
                name: "PuzzleAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PuzzleId = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    Seconds = table.Column<int>(type: "integer", nullable: false),
                    WrongMoves = table.Column<int>(type: "integer", nullable: false),
                    UsedHint = table.Column<bool>(type: "boolean", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: true),
                    PlayedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PuzzleAttempts_Puzzles_PuzzleId",
                        column: x => x.PuzzleId,
                        principalTable: "Puzzles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Puzzles_PuzzleDate",
                table: "Puzzles",
                column: "PuzzleDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleAttempts_ClientId_PuzzleId",
                table: "PuzzleAttempts",
                columns: new[] { "ClientId", "PuzzleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleAttempts_PuzzleId",
                table: "PuzzleAttempts",
                column: "PuzzleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PuzzleAttempts");

            migrationBuilder.DropIndex(
                name: "IX_Puzzles_PuzzleDate",
                table: "Puzzles");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Puzzles");

            migrationBuilder.DropColumn(
                name: "PuzzleDate",
                table: "Puzzles");
        }
    }
}
