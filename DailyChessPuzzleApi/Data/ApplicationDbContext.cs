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

        public DbSet<PuzzleAttempt> PuzzleAttempts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Puzzle>()
                .HasIndex(p => p.PuzzleDate)
                .IsUnique();

            modelBuilder.Entity<PuzzleAttempt>()
                .HasOne(a => a.Puzzle)
                .WithMany()
                .HasForeignKey(a => a.PuzzleId);

            modelBuilder.Entity<PuzzleAttempt>()
                .HasIndex(a => new { a.ClientId, a.PuzzleId })
                .IsUnique();
        }
    }
}