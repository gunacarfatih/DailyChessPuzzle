
using System;
using DailyChessPuzzleApi.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DailyChessPuzzleApi.Migrations
{
    public partial class AddLeaderboardScoreRevision : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski Outbox tablosunu ve tamamlanmış 8 kaydını kaldır.
            migrationBuilder.DropTable(
                name: "LeaderboardCacheOutbox");

            // Leaderboard skorlarına revision alanını ekle.
            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "LeaderboardScores",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Yeni ana Outbox tablosunu oluştur.
            migrationBuilder.CreateTable(
                name: "LeaderboardCacheOutbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    PuzzleAttemptId = table.Column<int>(type: "integer", nullable: false),

                    ClientId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false),

                    CreatedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_LeaderboardCacheOutbox",
                        x => x.Id);

                    table.ForeignKey(
                        name: "FK_LeaderboardCacheOutbox_PuzzleAttempts_PuzzleAttemptId",
                        column: x => x.PuzzleAttemptId,
                        principalTable: "PuzzleAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Her PuzzleAttempt için yalnızca bir ana Outbox olayı.
            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardCacheOutbox_PuzzleAttemptId",
                table: "LeaderboardCacheOutbox",
                column: "PuzzleAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardCacheOutbox_CreatedAtUtc",
                table: "LeaderboardCacheOutbox",
                column: "CreatedAtUtc");

            // Bağımsız Redis teslimatları tablosunu oluştur.
            migrationBuilder.CreateTable(
                name: "LeaderboardCacheOutboxDeliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    OutboxId = table.Column<int>(type: "integer", nullable: false),

                    Period = table.Column<string>(
                        type: "character varying(10)",
                        maxLength: 10,
                        nullable: false),

                    PeriodKey = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false),

                    CountryCode = table.Column<string>(
                        type: "character varying(2)",
                        maxLength: 2,
                        nullable: true),

                    CacheKey = table.Column<string>(
                        type: "character varying(300)",
                        maxLength: 300,
                        nullable: false),

                    TotalScore = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    SolvedCount = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    Status = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    RetryCount = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    LastError = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true),

                    NextRetryAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true),

                    LockedUntilUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true),

                    LockedBy = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true),

                    CreatedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false),

                    ProcessedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_LeaderboardCacheOutboxDeliveries",
                        x => x.Id);

                    table.ForeignKey(
                        name: "FK_LeaderboardCacheOutboxDeliveries_LeaderboardCacheOutbox_OutboxId",
                        column: x => x.OutboxId,
                        principalTable: "LeaderboardCacheOutbox",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardCacheOutboxDeliveries_OutboxId_Period_PeriodKey",
                table: "LeaderboardCacheOutboxDeliveries",
                columns: new[]
                {
                    "OutboxId",
                    "Period",
                    "PeriodKey"
                },
                unique: true,
                filter: "\"CountryCode\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardCacheOutboxDeliveries_OutboxId_Period_PeriodKey_CountryCode",
                table: "LeaderboardCacheOutboxDeliveries",
                columns: new[]
                {
                    "OutboxId",
                    "Period",
                    "PeriodKey",
                    "CountryCode"
                },
                unique: true,
                filter: "\"CountryCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardCacheOutboxDeliveries_Status_NextRetryAtUtc_CreatedAtUtc",
                table: "LeaderboardCacheOutboxDeliveries",
                columns: new[]
                {
                    "Status",
                    "NextRetryAtUtc",
                    "CreatedAtUtc"
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Yeni teslimat tablosunu kaldır.
            migrationBuilder.DropTable(
                name: "LeaderboardCacheOutboxDeliveries");

            // Yeni ana Outbox tablosunu kaldır.
            migrationBuilder.DropTable(
                name: "LeaderboardCacheOutbox");

            // Revision alanını kaldır.
            migrationBuilder.DropColumn(
                name: "Revision",
                table: "LeaderboardScores");

            // Eski Outbox tablosunun yapısını geri oluştur.
            migrationBuilder.CreateTable(
                name: "LeaderboardCacheOutbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),

                    ClientId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false),

                    CreatedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false),

                    CacheKey = table.Column<string>(
                        type: "character varying(300)",
                        maxLength: 300,
                        nullable: false),

                    IsProcessed = table.Column<bool>(
                        type: "boolean",
                        nullable: false),

                    Period = table.Column<string>(
                        type: "character varying(10)",
                        maxLength: 10,
                        nullable: false),

                    PeriodKey = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false),

                    CountryCode = table.Column<string>(
                        type: "character varying(2)",
                        maxLength: 2,
                        nullable: true),

                    TotalScore = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    SolvedCount = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    ProcessedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_LeaderboardCacheOutbox",
                        x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardCacheOutbox_IsProcessed_CreatedAtUtc",
                table: "LeaderboardCacheOutbox",
                columns: new[]
                {
                    "IsProcessed",
                    "CreatedAtUtc"
                });
        }
    }
}
