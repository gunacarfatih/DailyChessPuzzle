
using System.Globalization;
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

        // Bulmaca sonucunu hesaplar ve PostgreSQL'e kaydeder.
        // Leaderboard güncelleme görevlerini de aynı transaction
        // içerisinde Outbox tablosuna ekler.
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

            // Puan hesaplaması.
            long timePenalty = (long)request.Seconds * 2;
            long wrongMovePenalty = (long)request.WrongMoves * 150;
            long hintPenalty = request.UsedHint ? 250 : 0;

            int score = (int)Math.Max(
                0L,
                1000L - timePenalty - wrongMovePenalty - hintPenalty
            );

            string? countryCode = string.IsNullOrWhiteSpace(request.CountryCode)
                ? null
                : request.CountryCode.Trim().ToUpperInvariant();

            var attempt = new PuzzleAttempt
            {
                PuzzleId = puzzle.Id,
                ClientId = request.ClientId,
                Seconds = request.Seconds,
                WrongMoves = request.WrongMoves,
                UsedHint = request.UsedHint,
                Score = score,
                CountryCode = countryCode,
                PlayedOn = today,
                SubmittedAtUtc = DateTime.UtcNow
            };

            // PuzzleAttempt ve Outbox kayıtlarının birlikte
            // kaydedilmesini garanti etmek için transaction başlatılır.
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    cancellationToken
                );

            try
            {
                // 1. Bulmaca denemesini PostgreSQL'e ekle.
                await _context.PuzzleAttempts.AddAsync(
                    attempt,
                    cancellationToken
                );

                await _context.SaveChangesAsync(cancellationToken);

                // 2. Dört leaderboard dönemi için görevleri oluştur.
                await CreateLeaderboardOutboxEntriesAsync(
                    request.ClientId,
                    today,
                    countryCode,
                    cancellationToken
                );

                // 3. Outbox görevlerini PostgreSQL'e kaydet.
                await _context.SaveChangesAsync(cancellationToken);

                // Her iki kayıt da başarılıysa transaction tamamlanır.
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await transaction.RollbackAsync(cancellationToken);

                _logger.LogWarning(
                    "Duplicate puzzle submission detected by database constraint. PuzzleId: {PuzzleId}",
                    request.PuzzleId
                );

                throw new InvalidOperationException(
                    "You have already submitted this puzzle.",
                    ex
                );
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            _logger.LogInformation(
                "Puzzle submission and leaderboard outbox tasks saved successfully. PuzzleId: {PuzzleId}, Score: {Score}",
                puzzle.Id,
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

        // Günlük, haftalık, aylık ve tüm zamanlar leaderboard
        // görevlerini oluşturur.
        private async Task CreateLeaderboardOutboxEntriesAsync(
            string clientId,
            DateOnly today,
            string? countryCode,
            CancellationToken cancellationToken)
        {
            // Günlük dönem.
            var dailyPeriod = (
                Period: "daily",
                PeriodKey: today.ToString("yyyy-MM-dd"),
                StartDate: today
            );

            // Haftalık dönem: ISO standardına göre pazartesi başlangıcı.
            int daysSinceMonday =
                ((int)today.DayOfWeek + 6) % 7;

            var weeklyStartDate = today.AddDays(-daysSinceMonday);

            int isoYear = ISOWeek.GetYear(
                today.ToDateTime(TimeOnly.MinValue)
            );

            int isoWeek = ISOWeek.GetWeekOfYear(
                today.ToDateTime(TimeOnly.MinValue)
            );

            var weeklyPeriod = (
                Period: "weekly",
                PeriodKey: $"{isoYear}-W{isoWeek:D2}",
                StartDate: weeklyStartDate
            );

            // Aylık dönem: takvim ayının ilk günü.
            var monthlyStartDate = new DateOnly(
                today.Year,
                today.Month,
                1
            );

            var monthlyPeriod = (
                Period: "monthly",
                PeriodKey: today.ToString("yyyy-MM"),
                StartDate: monthlyStartDate
            );

            // Tüm zamanlar dönemi.
            // Başlangıç tarihi DateOnly.MinValue olarak belirlenir.
            var allTimePeriod = (
                Period: "alltime",
                PeriodKey: "all",
                StartDate: DateOnly.MinValue
            );

            var periods = new[]
            {
                dailyPeriod,
                weeklyPeriod,
                monthlyPeriod,
                allTimePeriod
            };

            foreach (var period in periods)
            {
                // Global leaderboard görevi.
                await CreateLeaderboardOutboxEntryAsync(
                    period.Period,
                    period.PeriodKey,
                    period.StartDate,
                    today,
                    clientId,
                    null,
                    cancellationToken
                );

                // Kullanıcının ülke bilgisi varsa ülke bazlı görev.
                if (countryCode != null)
                {
                    await CreateLeaderboardOutboxEntryAsync(
                        period.Period,
                        period.PeriodKey,
                        period.StartDate,
                        today,
                        clientId,
                        countryCode,
                        cancellationToken
                    );
                }
            }
        }

        // Kullanıcının ilgili dönemdeki toplam puanını hesaplar
        // ve PostgreSQL Outbox tablosuna ekler.
        private async Task CreateLeaderboardOutboxEntryAsync(
            string period,
            string periodKey,
            DateOnly startDate,
            DateOnly today,
            string clientId,
            string? countryCode,
            CancellationToken cancellationToken)
        {
            // Kullanıcının ilgili dönemdeki denemelerini getir.
            var query = _context.PuzzleAttempts
                .AsNoTracking()
                .Where(a =>
                    a.ClientId == clientId &&
                    a.PlayedOn >= startDate &&
                    a.PlayedOn <= today
                );

            // Ülke bazlı leaderboard isteniyorsa ülkeye göre filtrele.
            // CountryCode null ise tüm ülkelerdeki denemeler hesaba katılır.
            if (countryCode != null)
            {
                query = query.Where(
                    a => a.CountryCode == countryCode
                );
            }

            // Kullanıcının toplam puanını ve çözdüğü puzzle sayısını hesapla.
            var aggregate = await query
                .GroupBy(a => a.ClientId)
                .Select(group => new
                {
                    TotalScore = group.Sum(a => a.Score),
                    SolvedCount = group.Count()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (aggregate == null)
            {
                return;
            }

            // Redis anahtar formatı.
            // Örnek:
            // DailyChessPuzzle_leaderboard:daily:2026-11-09:global
            // DailyChessPuzzle_leaderboard:alltime:all:global
            string cacheKey =
                $"DailyChessPuzzle_leaderboard:{period}:{periodKey}:{countryCode ?? "global"}";

            // PostgreSQL Outbox tablosuna güncelleme görevi ekle.
            var outboxEntry = new LeaderboardCacheOutbox
            {
                CacheKey = cacheKey,
                ClientId = clientId,
                Period = period,
                PeriodKey = periodKey,
                CountryCode = countryCode,
                TotalScore = aggregate.TotalScore,
                SolvedCount = aggregate.SolvedCount,
                IsProcessed = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _context.LeaderboardCacheOutbox.AddAsync(
                outboxEntry,
                cancellationToken
            );

            _logger.LogInformation(
                "Leaderboard outbox task created. Period: {Period}, PeriodKey: {PeriodKey}, ClientId: {ClientId}, Country: {Country}",
                period,
                periodKey,
                clientId,
                countryCode ?? "Global"
            );
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
