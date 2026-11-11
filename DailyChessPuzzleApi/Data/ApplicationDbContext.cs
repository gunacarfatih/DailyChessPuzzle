
using DailyChessPuzzleApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChessPuzzleApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Puzzle> Puzzles { get; set; }
        public DbSet<DailyPuzzle> DailyPuzzles { get; set; }
        public DbSet<PuzzleAttempt> PuzzleAttempts { get; set; }

        // Ana Outbox olayları.
        public DbSet<LeaderboardCacheOutbox> LeaderboardCacheOutbox { get; set; }

        // Her Outbox olayına bağlı bağımsız teslimatlar.
        public DbSet<LeaderboardCacheOutboxDelivery> LeaderboardCacheOutboxDeliveries { get; set; }

        // Kullanıcıların dönemsel leaderboard puan özetleri.
        public DbSet<LeaderboardScore> LeaderboardScores { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Puzzle>()
                .HasIndex(x => x.LichessPuzzleId)
                .IsUnique();

            modelBuilder.Entity<DailyPuzzle>()
                .HasIndex(x => x.PuzzleDate)
                .IsUnique();

            modelBuilder.Entity<DailyPuzzle>()
                .HasOne(x => x.Puzzle)
                .WithMany(x => x.DailyPuzzles)
                .HasForeignKey(x => x.PuzzleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PuzzleAttempt>()
                .HasOne(x => x.Puzzle)
                .WithMany()
                .HasForeignKey(x => x.PuzzleId);

            modelBuilder.Entity<PuzzleAttempt>()
                .HasIndex(x => new { x.ClientId, x.PuzzleId })
                .IsUnique();

            modelBuilder.Entity<PuzzleAttempt>()
                .HasIndex(x => x.PlayedOn);

            modelBuilder.Entity<PuzzleAttempt>()
                .HasIndex(x => new { x.CountryCode, x.PlayedOn });

            // -----------------------------------------
            // LEADERBOARD OUTBOX
            // -----------------------------------------

            modelBuilder.Entity<LeaderboardCacheOutbox>()
                .HasIndex(x => x.PuzzleAttemptId)
                .IsUnique();

            modelBuilder.Entity<LeaderboardCacheOutbox>()
                .HasIndex(x => x.CreatedAtUtc);

            modelBuilder.Entity<LeaderboardCacheOutbox>()
                .Property(x => x.ClientId)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<LeaderboardCacheOutbox>()
                .HasOne<PuzzleAttempt>()
                .WithMany()
                .HasForeignKey(x => x.PuzzleAttemptId)
                .OnDelete(DeleteBehavior.Restrict);

            // -----------------------------------------
            // LEADERBOARD OUTBOX DELIVERY
            // -----------------------------------------

            modelBuilder.Entity<LeaderboardCacheOutboxDelivery>()
                .HasOne(x => x.Outbox)
                .WithMany(x => x.Deliveries)
                .HasForeignKey(x => x.OutboxId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LeaderboardCacheOutboxDelivery>()
                .Property(x => x.Period)
                .HasMaxLength(10)
                .IsRequired();

            modelBuilder.Entity<LeaderboardCacheOutboxDelivery>()
                .Property(x => x.PeriodKey)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<LeaderboardCacheOutboxDelivery>()
                .Property(x => x.CountryCode)
                .HasMaxLength(2);

            modelBuilder.Entity<LeaderboardCacheOutboxDelivery>()
                .Property(x => x.CacheKey)
                .HasMaxLength(300)
                .IsRequired();

            modelBuilder.Entity<LeaderboardCacheOutboxDelivery>()
                .Property(x => x.LastError)
                .HasMaxLength(2000);

            modelBuilder.Entity<LeaderboardCacheOutboxDelivery>()
                .Property(x => x.LockedBy)
                .HasMaxLength(100);

            modelBuilder.Entity<LeaderboardCacheOutboxDelivery>()
                .HasIndex(x => new
                {
                    x.Status,
                    x.NextRetryAtUtc,
                    x.CreatedAtUtc
                });

            modelBuilder.Entity<LeaderboardCacheOutboxDelivery>()
                .HasIndex(x => new
                {
                    x.OutboxId,
                    x.Period,
                    x.PeriodKey
                })
                .IsUnique()
                .HasFilter("\"CountryCode\" IS NULL");

            modelBuilder.Entity<LeaderboardCacheOutboxDelivery>()
                .HasIndex(x => new
                {
                    x.OutboxId,
                    x.Period,
                    x.PeriodKey,
                    x.CountryCode
                })
                .IsUnique()
                .HasFilter("\"CountryCode\" IS NOT NULL");

            // -----------------------------------------
            // LEADERBOARD SCORE
            // -----------------------------------------

            modelBuilder.Entity<LeaderboardScore>()
                .Property(x => x.ClientId)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<LeaderboardScore>()
                .Property(x => x.Period)
                .HasMaxLength(10)
                .IsRequired();

            modelBuilder.Entity<LeaderboardScore>()
                .Property(x => x.PeriodKey)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<LeaderboardScore>()
                .Property(x => x.CountryCode)
                .HasMaxLength(2);

            // Yeni Revision alanının varsayılan değeri.
            modelBuilder.Entity<LeaderboardScore>()
                .Property(x => x.Revision)
                .HasDefaultValue(0L)
                .IsRequired();

            modelBuilder.Entity<LeaderboardScore>()
                .HasIndex(x => new
                {
                    x.ClientId,
                    x.Period,
                    x.PeriodKey
                })
                .IsUnique()
                .HasFilter("\"CountryCode\" IS NULL");

            modelBuilder.Entity<LeaderboardScore>()
                .HasIndex(x => new
                {
                    x.ClientId,
                    x.Period,
                    x.PeriodKey,
                    x.CountryCode
                })
                .IsUnique()
                .HasFilter("\"CountryCode\" IS NOT NULL");

            modelBuilder.Entity<LeaderboardScore>()
                .HasIndex(x => new
                {
                    x.Period,
                    x.PeriodKey,
                    x.CountryCode,
                    x.TotalScore,
                    x.SolvedCount
                });
        }
    }
}
