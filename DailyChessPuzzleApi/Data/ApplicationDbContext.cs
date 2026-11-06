using DailyChessPuzzleApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DailyChessPuzzleApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Puzzle> Puzzles { get; set; }

        public DbSet<DailyPuzzle> DailyPuzzles { get; set; }

        public DbSet<PuzzleAttempt> PuzzleAttempts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Her Lichess bulmacasının kimliği benzersiz olmalı.
            modelBuilder.Entity<Puzzle>()
                .HasIndex(p => p.LichessPuzzleId)
                .IsUnique();

            // Aynı tarihe yalnızca bir günlük bulmaca atanabilir.
            modelBuilder.Entity<DailyPuzzle>()
                .HasIndex(dp => dp.PuzzleDate)
                .IsUnique();

            // DailyPuzzle - Puzzle ilişkisi
            modelBuilder.Entity<DailyPuzzle>()
                .HasOne(dp => dp.Puzzle)
                .WithMany(p => p.DailyPuzzles)
                .HasForeignKey(dp => dp.PuzzleId)
                .OnDelete(DeleteBehavior.Restrict);

            // PuzzleAttempt - Puzzle ilişkisi
            modelBuilder.Entity<PuzzleAttempt>()
                .HasOne(a => a.Puzzle)
                .WithMany()
                .HasForeignKey(a => a.PuzzleId);

            // Aynı kullanıcı aynı bulmacayı iki kez gönderememeli.
            modelBuilder.Entity<PuzzleAttempt>()
                .HasIndex(a => new { a.ClientId, a.PuzzleId })
                .IsUnique();
        }
    }
}