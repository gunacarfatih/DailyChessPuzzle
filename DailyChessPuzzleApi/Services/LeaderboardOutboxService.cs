
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
            // Henüz işlenmemiş görevleri getir.
            var pendingTasks = await _context.LeaderboardCacheOutbox
                .Where(x => !x.IsProcessed)
                .OrderBy(x => x.CreatedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            foreach (var task in pendingTasks)
            {
                try
                {
                    // Kullanıcının ilgili dönem ve ülke için
                    // mevcut leaderboard kaydını bul.
                    var leaderboardScore = await _context.LeaderboardScores
                        .FirstOrDefaultAsync(x =>
                            x.ClientId == task.ClientId &&
                            x.Period == task.Period &&
                            x.PeriodKey == task.PeriodKey &&
                            x.CountryCode == task.CountryCode,
                            cancellationToken
                        );

                    if (leaderboardScore == null)
                    {
                        // İlk kayıt ise yeni leaderboard satırı oluştur.
                        leaderboardScore = new LeaderboardScore
                        {
                            ClientId = task.ClientId,
                            Period = task.Period,
                            PeriodKey = task.PeriodKey,
                            CountryCode = task.CountryCode,
                            TotalScore = task.TotalScore,
                            SolvedCount = task.SolvedCount,
                            UpdatedAtUtc = DateTime.UtcNow
                        };

                        await _context.LeaderboardScores.AddAsync(
                            leaderboardScore,
                            cancellationToken
                        );
                    }
                    else
                    {
                        // Eski bir Outbox görevinin daha güncel veriyi
                        // geriye götürmesini engelle.
                        if (task.SolvedCount >= leaderboardScore.SolvedCount)
                        {
                            // Toplam puanı doğrudan güncelle.
                            // Böylece aynı görev tekrar işlenirse
                            // puanlar iki kez eklenmez.
                            leaderboardScore.TotalScore = task.TotalScore;
                            leaderboardScore.SolvedCount = task.SolvedCount;
                            leaderboardScore.UpdatedAtUtc = DateTime.UtcNow;
                        }
                    }

                    // Redis güncellemesi.
                    // Günlük leaderboard için ayrıca versiyon artırılır.
                    if (task.Period.Equals(
                        "daily",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        // Günlük leaderboard versiyon anahtarı.
                        string versionKey = $"{task.CacheKey}:version";

                        await _cacheService.UpdateDailyScoreAsync(
                            task.CacheKey,
                            versionKey,
                            task.ClientId,
                            task.TotalScore,
                            task.SolvedCount
                        );
                    }
                    else
                    {
                        // Haftalık, aylık ve tüm zamanlar.
                        await _cacheService.UpdateScoreAsync(
                            task.CacheKey,
                            task.ClientId,
                            task.TotalScore,
                            task.SolvedCount
                        );
                    }

                    // Redis güncellemesi başarılı olduktan sonra
                    // Outbox görevini tamamlandı olarak işaretle.
                    task.IsProcessed = true;
                    task.ProcessedAtUtc = DateTime.UtcNow;

                    // PostgreSQL leaderboard kaydı ve Outbox durumu
                    // aynı SaveChanges çağrısında kaydedilir.
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Leaderboard güncellendi. Period: {Period}, ClientId: {ClientId}, Country: {Country}",
                        task.Period,
                        task.ClientId,
                        task.CountryCode ?? "Global"
                    );
                }
                catch (Exception ex)
                {
                    // Redis veya PostgreSQL işlemi başarısızsa
                    // görev tamamlanmış sayılmaz.
                    _logger.LogError(
                        ex,
                        "Leaderboard Outbox görevi işlenemedi. TaskId: {TaskId}",
                        task.Id
                    );

                    // Sıralı işlemeyi korumak için sonraki görevlere geçme.
                    break;
                }
            }
        }
    }
}
