
using DailyChessPuzzleApi.Data;
using DailyChessPuzzleApi.DTOs;
using DailyChessPuzzleApi.Models;
using DailyChessPuzzleApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DailyChessPuzzleApi.Services
{
    public class PuzzleService : IPuzzleService
    {
        private readonly ApplicationDbContext _context;

        public PuzzleService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Günün bulmacasını getirir.
        public async Task<PuzzleTodayResponse?> GetTodayAsync(
            string clientId,
            CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var puzzle = await _context.Puzzles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.PuzzleDate == today && p.IsActive,
                    cancellationToken
                );

            if (puzzle == null)
                return null;

            var isSolved = await _context.PuzzleAttempts
                .AnyAsync(
                    a => a.PuzzleId == puzzle.Id && a.ClientId == clientId,
                    cancellationToken
                );

            return new PuzzleTodayResponse
            {
                Id = puzzle.Id,
                Fen = puzzle.Fen,
                Moves = puzzle.Moves,
                Difficulty = puzzle.Difficulty,
                IsSolved = isSolved
            };
        }

        // Bulmaca sonucunu hesaplar ve kaydeder.
        public async Task<PuzzleSubmitResponse> SubmitAsync(
            PuzzleSubmitRequest request,
            CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var puzzle = await _context.Puzzles
                .FirstOrDefaultAsync(
                    p => p.Id == request.PuzzleId &&
                         p.PuzzleDate == today &&
                         p.IsActive,
                    cancellationToken
                );

            if (puzzle == null)
                throw new KeyNotFoundException("Today's puzzle was not found.");

            var alreadySolved = await _context.PuzzleAttempts
                .AnyAsync(
                    a => a.PuzzleId == request.PuzzleId &&
                         a.ClientId == request.ClientId,
                    cancellationToken
                );

            if (alreadySolved)
                throw new InvalidOperationException(
                    "You have already submitted this puzzle."
                );

            long timePenalty = (long)request.Seconds * 2;
            long wrongMovePenalty = (long)request.WrongMoves * 150;
            long hintPenalty = request.UsedHint ? 250 : 0;

            int score = (int)Math.Max(
                0L,
                1000L - timePenalty - wrongMovePenalty - hintPenalty
            );

            var attempt = new PuzzleAttempt
            {
                PuzzleId = puzzle.Id,
                ClientId = request.ClientId,
                Seconds = request.Seconds,
                WrongMoves = request.WrongMoves,
                UsedHint = request.UsedHint,
                Score = score,
                CountryCode = request.CountryCode?.ToUpperInvariant(),
                PlayedOn = today,
                SubmittedAtUtc = DateTime.UtcNow
            };

            try
            {
                await _context.PuzzleAttempts.AddAsync(
                    attempt,
                    cancellationToken
                );

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new InvalidOperationException(
                    "You have already submitted this puzzle.",
                    ex
                );
            }

            return new PuzzleSubmitResponse
            {
                Score = score,
                Seconds = request.Seconds,
                WrongMoves = request.WrongMoves,
                UsedHint = request.UsedHint,
                Message = "Puzzle result saved successfully."
            };
        }
    }
}
