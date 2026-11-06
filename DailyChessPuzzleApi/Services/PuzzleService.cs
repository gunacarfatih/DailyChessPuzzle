
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
        private readonly ILogger<PuzzleService> _logger;

        public PuzzleService(
            ApplicationDbContext context,
            ILogger<PuzzleService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Günün bulmacasını getirir.
        public async Task<PuzzleTodayResponse?> GetTodayAsync(
            string clientId,
            CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var puzzle = await _context.DailyPuzzles
                .AsNoTracking()
                .Where(dp =>
                    dp.PuzzleDate == today &&
                    dp.Puzzle.IsActive)
                .Select(dp => dp.Puzzle)
                .FirstOrDefaultAsync(cancellationToken);

            if (puzzle == null)
            {
                _logger.LogWarning(
                    "No active puzzle found for today: {PuzzleDate}",
                    today
                );

                return null;
            }

            var isSolved = await _context.PuzzleAttempts
                .AnyAsync(
                    a => a.PuzzleId == puzzle.Id &&
                         a.ClientId == clientId,
                    cancellationToken
                );

            _logger.LogInformation(
                "Today's puzzle retrieved successfully. PuzzleId: {PuzzleId}, IsSolved: {IsSolved}",
                puzzle.Id,
                isSolved
            );

            return new PuzzleTodayResponse
            {
                Id = puzzle.Id,
                Fen = puzzle.Fen,
                Moves = puzzle.Moves,
                Difficulty = GetDifficulty(puzzle.Rating),
                IsSolved = isSolved
            };
        }

        // Bulmaca sonucunu hesaplar ve kaydeder.
        public async Task<PuzzleSubmitResponse> SubmitAsync(
            PuzzleSubmitRequest request,
            CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var puzzle = await _context.DailyPuzzles
                .Where(dp =>
                    dp.PuzzleId == request.PuzzleId &&
                    dp.PuzzleDate == today &&
                    dp.Puzzle.IsActive)
                .Select(dp => dp.Puzzle)
                .FirstOrDefaultAsync(cancellationToken);

            if (puzzle == null)
            {
                _logger.LogWarning(
                    "Puzzle submission failed. Puzzle not found. PuzzleId: {PuzzleId}",
                    request.PuzzleId
                );

                throw new KeyNotFoundException(
                    "Today's puzzle was not found."
                );
            }

            var alreadySolved = await _context.PuzzleAttempts
                .AnyAsync(
                    a => a.PuzzleId == request.PuzzleId &&
                         a.ClientId == request.ClientId,
                    cancellationToken
                );

            if (alreadySolved)
            {
                _logger.LogWarning(
                    "Duplicate puzzle submission detected. PuzzleId: {PuzzleId}",
                    request.PuzzleId
                );

                throw new InvalidOperationException(
                    "You have already submitted this puzzle."
                );
            }

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
                _logger.LogWarning(
                    "Duplicate puzzle submission detected by database constraint. PuzzleId: {PuzzleId}",
                    request.PuzzleId
                );

                throw new InvalidOperationException(
                    "You have already submitted this puzzle.",
                    ex
                );
            }

            _logger.LogInformation(
                "Puzzle submission saved successfully. PuzzleId: {PuzzleId}, Score: {Score}",
                request.PuzzleId,
                score
            );

            return new PuzzleSubmitResponse
            {
                Score = score,
                Seconds = request.Seconds,
                WrongMoves = request.WrongMoves,
                UsedHint = request.UsedHint,
                Message = "Puzzle result saved successfully."
            };
        }

        // Rating değerinden zorluk seviyesini belirler.
        private static string GetDifficulty(int rating)
        {
            if (rating <= 1400)
                return "Kolay";

            if (rating <= 2000)
                return "Orta";

            return "Zor";
        }
    }
}
