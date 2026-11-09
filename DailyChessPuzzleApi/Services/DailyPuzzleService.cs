using DailyChessPuzzleApi.Data;
using DailyChessPuzzleApi.Models;
using DailyChessPuzzleApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DailyChessPuzzleApi.Services
{
    public class DailyPuzzleService : IDailyPuzzleService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DailyPuzzleService> _logger;

        public DailyPuzzleService(
            ApplicationDbContext context,
            ILogger<DailyPuzzleService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Belirtilen tarih için günlük bulmaca atar.
        public async Task AssignDailyPuzzleAsync(
            DateOnly puzzleDate,
            CancellationToken cancellationToken = default)
        {
            // 1. Bu tarih için daha önce atama yapılmış mı?
            var alreadyAssigned = await _context.DailyPuzzles
                .AnyAsync(
                    dp => dp.PuzzleDate == puzzleDate,
                    cancellationToken
                );

            if (alreadyAssigned)
            {
                _logger.LogInformation(
                    "Daily puzzle already assigned for {PuzzleDate}.",
                    puzzleDate
                );

                return;
            }

            // 2. Daha önce günlük olarak kullanılmamış aktif bir bulmaca seç.
            var puzzle = await _context.Puzzles
                .Where(p =>
                    p.IsActive &&
                    !_context.DailyPuzzles.Any(
                        dp => dp.PuzzleId == p.Id
                    ))
                .OrderBy(p => EF.Functions.Random())
                .FirstOrDefaultAsync(cancellationToken);

            // 3. Kullanılabilir bulmaca bulunamadıysa hata oluştur.
            if (puzzle == null)
            {
                _logger.LogWarning(
                    "No available active puzzle found for {PuzzleDate}.",
                    puzzleDate
                );

                throw new InvalidOperationException(
                    "No available puzzles found."
                );
            }

            // 4. Günlük bulmaca atamasını oluştur.
            var dailyPuzzle = new DailyPuzzle
            {
                PuzzleId = puzzle.Id,
                PuzzleDate = puzzleDate
            };

            await _context.DailyPuzzles.AddAsync(
                dailyPuzzle,
                cancellationToken
            );

            try
            {
                // 5. Atamayı veritabanına kaydet.
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Daily puzzle assigned successfully. PuzzleId: {PuzzleId}, Date: {PuzzleDate}",
                    puzzle.Id,
                    puzzleDate
                );
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
                postgresException.ConstraintName == "IX_DailyPuzzles_PuzzleDate")
            {
                // Aynı tarih için eş zamanlı başka bir işlem kayıt oluşturmuş olabilir.
                _logger.LogInformation(
                    "Daily puzzle was assigned by another request. Date: {PuzzleDate}",
                    puzzleDate
                );
            }
        }
    }
}