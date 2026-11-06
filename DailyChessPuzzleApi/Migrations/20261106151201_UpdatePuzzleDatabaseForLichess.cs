using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DailyChessPuzzleApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePuzzleDatabaseForLichess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Puzzles_PuzzleDate",
                table: "Puzzles");

            migrationBuilder.DropColumn(
                name: "PuzzleDate",
                table: "Puzzles");

            migrationBuilder.RenameColumn(
                name: "Difficulty",
                table: "Puzzles",
                newName: "Themes");

            migrationBuilder.AddColumn<string>(
                name: "LichessPuzzleId",
                table: "Puzzles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Popularity",
                table: "Puzzles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "Puzzles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DailyPuzzles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PuzzleId = table.Column<int>(type: "integer", nullable: false),
                    PuzzleDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPuzzles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyPuzzles_Puzzles_PuzzleId",
                        column: x => x.PuzzleId,
                        principalTable: "Puzzles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Puzzles_LichessPuzzleId",
                table: "Puzzles",
                column: "LichessPuzzleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyPuzzles_PuzzleDate",
                table: "DailyPuzzles",
                column: "PuzzleDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyPuzzles_PuzzleId",
                table: "DailyPuzzles",
                column: "PuzzleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyPuzzles");

            migrationBuilder.DropIndex(
                name: "IX_Puzzles_LichessPuzzleId",
                table: "Puzzles");

            migrationBuilder.DropColumn(
                name: "LichessPuzzleId",
                table: "Puzzles");

            migrationBuilder.DropColumn(
                name: "Popularity",
                table: "Puzzles");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Puzzles");

            migrationBuilder.RenameColumn(
                name: "Themes",
                table: "Puzzles",
                newName: "Difficulty");

            migrationBuilder.AddColumn<DateOnly>(
                name: "PuzzleDate",
                table: "Puzzles",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "IX_Puzzles_PuzzleDate",
                table: "Puzzles",
                column: "PuzzleDate",
                unique: true);
        }
    }
}
