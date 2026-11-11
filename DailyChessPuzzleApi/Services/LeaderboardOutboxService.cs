
using DailyChessPuzzleApi.Data;
using DailyChessPuzzleApi.Models;
using DailyChessPuzzleApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyChessPuzzleApi.Services
{
    public class LeaderboardOutboxService : ILeaderboardOutboxService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeaderboardCacheService _cacheService;
        private readonly ILogger<LeaderboardOutboxService> _logger;

        private static readonly TimeSpan LockDuration =
            TimeSpan.FromMinutes(5);

        public LeaderboardOutboxService(
            ApplicationDbContext context,
            ILeaderboardCacheService cacheService,
            ILogger<LeaderboardOutboxService> logger)
        {
            _context = context;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task ProcessPendingAsync(
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var deliveryIds = await _context.LeaderboardCacheOutboxDeliveries
                .AsNoTracking()
                .Where(x =>
                    x.Status == LeaderboardCacheOutboxDeliveryStatus.Pending ||
                    (
                        x.Status == LeaderboardCacheOutboxDeliveryStatus.Failed &&
                        x.NextRetryAtUtc != null &&
                        x.NextRetryAtUtc <= now
                    ) ||
                    (
                        x.Status == LeaderboardCacheOutboxDeliveryStatus.Processing &&
                        x.LockedUntilUtc != null &&
                        x.LockedUntilUtc <= now
                    ))
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => x.Id)
                .Take(100)
                .ToListAsync(cancellationToken);

            foreach (var deliveryId in deliveryIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string lockId = Guid.NewGuid().ToString("N");

                bool lockAcquired = await TryAcquireLockAsync(
                    deliveryId,
                    lockId,
                    cancellationToken);

                if (!lockAcquired)
                {
                    continue;
                }

                var delivery = await _context.LeaderboardCacheOutboxDeliveries
                    .AsNoTracking()
                    .Include(x => x.Outbox)
                    .FirstOrDefaultAsync(
                        x => x.Id == deliveryId,
                        cancellationToken);

                if (delivery == null)
                {
                    continue;
                }

                try
                {
                    // Outbox ID, bu skor güncellemesinin revision değeridir.
                    long incomingRevision = delivery.OutboxId;

                    // Kullanıcının ilgili leaderboard kaydını atomik
                    // revision kontrolüyle güncelle.
                    var existingScore = await _context.LeaderboardScores
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.ClientId == delivery.Outbox.ClientId &&
                            x.Period == delivery.Period &&
                            x.PeriodKey == delivery.PeriodKey &&
                            x.CountryCode == delivery.CountryCode,
                            cancellationToken);

                    if (existingScore == null)
                    {
                        // İlk skor kaydı.
                        var newScore = new LeaderboardScore
                        {
                            ClientId = delivery.Outbox.ClientId,
                            Period = delivery.Period,
                            PeriodKey = delivery.PeriodKey,
                            CountryCode = delivery.CountryCode,
                            TotalScore = delivery.TotalScore,
                            SolvedCount = delivery.SolvedCount,
                            Revision = incomingRevision,
                            UpdatedAtUtc = DateTime.UtcNow
                        };

                        await _context.LeaderboardScores.AddAsync(
                            newScore,
                            cancellationToken);

                        await _context.SaveChangesAsync(cancellationToken);
                    }
                    else if (incomingRevision > existingScore.Revision)
                    {
                        // Yalnızca gelen revision daha yeniyse güncelle.
                        // ExecuteUpdateAsync işlemi PostgreSQL üzerinde
                        // tek bir atomik UPDATE sorgusu çalıştırır.
                        await _context.LeaderboardScores
                            .Where(x =>
                                x.Id == existingScore.Id &&
                                x.Revision < incomingRevision)
                            .ExecuteUpdateAsync(setters => setters
                                .SetProperty(
                                    x => x.TotalScore,
                                    delivery.TotalScore)
                                .SetProperty(
                                    x => x.SolvedCount,
                                    delivery.SolvedCount)
                                .SetProperty(
                                    x => x.Revision,
                                    incomingRevision)
                                .SetProperty(
                                    x => x.UpdatedAtUtc,
                                    DateTime.UtcNow),
                                cancellationToken);
                    }

                    // Yarış durumunda başka worker daha yeni bir revision
                    // yazmış olabilir. Bu nedenle Redis'e doğrudan Delivery
                    // verisini değil, PostgreSQL'deki güncel kaydı gönder.
                    var currentScore = await _context.LeaderboardScores
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.ClientId == delivery.Outbox.ClientId &&
                            x.Period == delivery.Period &&
                            x.PeriodKey == delivery.PeriodKey &&
                            x.CountryCode == delivery.CountryCode,
                            cancellationToken);

                    if (currentScore == null)
                    {
                        throw new InvalidOperationException(
                            "Leaderboard score could not be loaded after update.");
                    }

                    if (delivery.Period.Equals(
                        "daily",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        string versionKey = $"{delivery.CacheKey}:version";

                        await _cacheService.UpdateDailyScoreAsync(
                            delivery.CacheKey,
                            versionKey,
                            delivery.Outbox.ClientId,
                            currentScore.TotalScore,
                            currentScore.SolvedCount,
                            currentScore.Revision);
                    }
                    else
                    {
                        await _cacheService.UpdateScoreAsync(
                            delivery.CacheKey,
                            delivery.Outbox.ClientId,
                            currentScore.TotalScore,
                            currentScore.SolvedCount,
                            currentScore.Revision);
                    }

                    var processedAt = DateTime.UtcNow;

                    int completedRows = await _context
                        .LeaderboardCacheOutboxDeliveries
                        .Where(x =>
                            x.Id == deliveryId &&
                            x.Status == LeaderboardCacheOutboxDeliveryStatus.Processing &&
                            x.LockedBy == lockId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(
                                x => x.Status,
                                LeaderboardCacheOutboxDeliveryStatus.Completed)
                            .SetProperty(
                                x => x.ProcessedAtUtc,
                                processedAt)
                            .SetProperty(
                                x => x.NextRetryAtUtc,
                                (DateTime?)null)
                            .SetProperty(
                                x => x.LockedUntilUtc,
                                (DateTime?)null)
                            .SetProperty(
                                x => x.LockedBy,
                                (string?)null)
                            .SetProperty(
                                x => x.LastError,
                                (string?)null),
                            cancellationToken);

                    if (completedRows == 0)
                    {
                        _logger.LogWarning(
                            "Delivery completion skipped because the lock is no longer owned by this worker. DeliveryId: {DeliveryId}",
                            deliveryId);

                        continue;
                    }

                    _logger.LogInformation(
                        "Leaderboard Delivery completed. DeliveryId: {DeliveryId}, Period: {Period}, ClientId: {ClientId}, Country: {Country}, Revision: {Revision}",
                        delivery.Id,
                        delivery.Period,
                        delivery.Outbox.ClientId,
                        delivery.CountryCode ?? "Global",
                        currentScore.Revision);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Başarısız insert gibi durumlarda EF ChangeTracker'da
                    // hatalı Added kayıt kalmasını önle.
                    _context.ChangeTracker.Clear();

                    int retryCount = delivery.RetryCount + 1;

                    int retryDelayMinutes = Math.Min(
                        (int)Math.Pow(2, retryCount),
                        30);

                    string errorMessage = ex.Message.Length > 2000
                        ? ex.Message[..2000]
                        : ex.Message;

                    var nextRetryAt = DateTime.UtcNow.AddMinutes(
                        retryDelayMinutes);

                    try
                    {
                        await _context
                            .LeaderboardCacheOutboxDeliveries
                            .Where(x =>
                                x.Id == deliveryId &&
                                x.Status == LeaderboardCacheOutboxDeliveryStatus.Processing &&
                                x.LockedBy == lockId)
                            .ExecuteUpdateAsync(setters => setters
                                .SetProperty(
                                    x => x.Status,
                                    LeaderboardCacheOutboxDeliveryStatus.Failed)
                                .SetProperty(
                                    x => x.RetryCount,
                                    retryCount)
                                .SetProperty(
                                    x => x.LastError,
                                    errorMessage)
                                .SetProperty(
                                    x => x.NextRetryAtUtc,
                                    nextRetryAt)
                                .SetProperty(
                                    x => x.LockedUntilUtc,
                                    (DateTime?)null)
                                .SetProperty(
                                    x => x.LockedBy,
                                    (string?)null),
                                cancellationToken);
                    }
                    catch (Exception saveException)
                    {
                        _logger.LogError(
                            saveException,
                            "Delivery failure state could not be saved. DeliveryId: {DeliveryId}",
                            deliveryId);
                    }

                    _logger.LogError(
                        ex,
                        "Leaderboard Delivery failed. DeliveryId: {DeliveryId}, RetryCount: {RetryCount}",
                        deliveryId,
                        retryCount);
                }
            }
        }

        // PostgreSQL üzerinde atomik kilit alma işlemi.
        private async Task<bool> TryAcquireLockAsync(
            int deliveryId,
            string lockId,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var lockUntil = now.Add(LockDuration);

            int affectedRows = await _context
                .LeaderboardCacheOutboxDeliveries
                .Where(x =>
                    x.Id == deliveryId &&
                    (
                        x.Status == LeaderboardCacheOutboxDeliveryStatus.Pending ||
                        (
                            x.Status == LeaderboardCacheOutboxDeliveryStatus.Failed &&
                            x.NextRetryAtUtc != null &&
                            x.NextRetryAtUtc <= now
                        ) ||
                        (
                            x.Status == LeaderboardCacheOutboxDeliveryStatus.Processing &&
                            x.LockedUntilUtc != null &&
                            x.LockedUntilUtc <= now
                        )
                    ))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        x => x.Status,
                        LeaderboardCacheOutboxDeliveryStatus.Processing)
                    .SetProperty(
                        x => x.LockedBy,
                        lockId)
                    .SetProperty(
                        x => x.LockedUntilUtc,
                        lockUntil)
                    .SetProperty(
                        x => x.LastError,
                        (string?)null),
                    cancellationToken);

            return affectedRows == 1;
        }
    }
}
