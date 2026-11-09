
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

        // Redis leaderboard güncelleme görevleri.
        public DbSet<LeaderboardCacheOutbox> LeaderboardCacheOutbox { get; set; }

        // Kullanıcıların dönemsel leaderboard puan özetleri.
        public DbSet<LeaderboardScore> LeaderboardScores { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Lichess puzzle ID benzersiz olmalı.
            modelBuilder.Entity<Puzzle>()
                .HasIndex(x => x.LichessPuzzleId)
                .IsUnique();

            // Her tarih için yalnızca bir günlük puzzle atanabilir.
            modelBuilder.Entity<DailyPuzzle>()
                .HasIndex(x => x.PuzzleDate)
                .IsUnique();

            // DailyPuzzle - Puzzle ilişkisi.
            modelBuilder.Entity<DailyPuzzle>()
                .HasOne(x => x.Puzzle)
                .WithMany(x => x.DailyPuzzles)
                .HasForeignKey(x => x.PuzzleId)
                .OnDelete(DeleteBehavior.Restrict);

            // PuzzleAttempt - Puzzle ilişkisi.
            modelBuilder.Entity<PuzzleAttempt>()
                .HasOne(x => x.Puzzle)
                .WithMany()
                .HasForeignKey(x => x.PuzzleId);

            // Bir kullanıcı aynı puzzle için yalnızca bir deneme kaydedebilir.
            modelBuilder.Entity<PuzzleAttempt>()
                .HasIndex(x => new { x.ClientId, x.PuzzleId })
                .IsUnique();

            // Denemelerin tarih bazlı sorgulanmasını hızlandırır.
            modelBuilder.Entity<PuzzleAttempt>()
                .HasIndex(x => x.PlayedOn);

            // Ülke ve tarih bazlı leaderboard sorgularını hızlandırır.
            modelBuilder.Entity<PuzzleAttempt>()
                .HasIndex(x => new { x.CountryCode, x.PlayedOn });

            // Bekleyen Outbox görevlerini hızlı bulabilmek için indeks.
            modelBuilder.Entity<LeaderboardCacheOutbox>()
                .HasIndex(x => new { x.IsProcessed, x.CreatedAtUtc });

            // Outbox dönem bilgisi zorunlu olsun.
            modelBuilder.Entity<LeaderboardCacheOutbox>()
                .Property(x => x.Period)
                .HasMaxLength(10)
                .IsRequired();

            // Dönem anahtarı zorunlu olsun.
            modelBuilder.Entity<LeaderboardCacheOutbox>()
                .Property(x => x.PeriodKey)
                .HasMaxLength(20)
                .IsRequired();

            // Ülke kodu en fazla 2 karakter olsun.
            // NULL olması global leaderboard kaydını ifade eder.
            modelBuilder.Entity<LeaderboardCacheOutbox>()
                .Property(x => x.CountryCode)
                .HasMaxLength(2);

            // Cache anahtarı zorunlu ve sınırlı uzunlukta olsun.
            modelBuilder.Entity<LeaderboardCacheOutbox>()
                .Property(x => x.CacheKey)
                .HasMaxLength(300)
                .IsRequired();

            // Kullanıcı kimliği zorunlu olsun.
            modelBuilder.Entity<LeaderboardCacheOutbox>()
                .Property(x => x.ClientId)
                .HasMaxLength(100)
                .IsRequired();

            // LeaderboardScore alanlarının uzunlukları.
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

            // Global leaderboard kayıtlarının benzersizliği.
            modelBuilder.Entity<LeaderboardScore>()
                .HasIndex(x => new { x.ClientId, x.Period, x.PeriodKey })
                .IsUnique()
                .HasFilter("\"CountryCode\" IS NULL");

            // Ülke bazlı leaderboard kayıtlarının benzersizliği.
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

            // Dönem ve ülke filtreli leaderboard sıralamalarını hızlandırır.
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
