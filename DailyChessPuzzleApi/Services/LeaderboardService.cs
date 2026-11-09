
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DailyChessPuzzleApi.Data;
using DailyChessPuzzleApi.DTOs;
using DailyChessPuzzleApi.Services.Interfaces;
using DailyChessPuzzleApi.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DailyChessPuzzleApi.Services
{
    public class LeaderboardService : ILeaderboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeaderboardCacheService _cacheService;
        private readonly ILogger<LeaderboardService> _logger;
        private readonly LeaderboardSettings _settings;

        private static readonly TimeSpan CustomRangeCacheDuration =
            TimeSpan.FromMinutes(60);

        public LeaderboardService(
            ApplicationDbContext context,
            ILeaderboardCacheService cacheService,
            ILogger<LeaderboardService> logger,
            IOptions<LeaderboardSettings> options)
        {
            _context = context;
            _cacheService = cacheService;
            _logger = logger;
            _settings = options.Value;
        }

        public async Task<LeaderboardResponse> GetLeaderboardAsync(
            string? country,
            string? clientId,
            int limit,
            DateOnly? startDate,
            DateOnly? endDate,
            CancellationToken cancellationToken = default)
        {
            // Limit değeri gönderilmemişse varsayılan değeri kullan.
            if (limit <= 0)
            {
                limit = _settings.DefaultLimit;
            }

            if (limit < _settings.MinLimit ||
                limit > _settings.MaxLimit)
            {
                throw new ArgumentException(
                    $"Limit must be between {_settings.MinLimit} and {_settings.MaxLimit}.");
            }

            // Ülke kodunu doğrula.
            country = ValidateCountry(country);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Özel tarih aralığı parametrelerinin ikisi birlikte gönderilmelidir.
            if (startDate.HasValue != endDate.HasValue)
            {
                throw new ArgumentException(
                    "StartDate and EndDate must be provided together.");
            }

            if (startDate.HasValue && endDate.HasValue)
            {
                if (startDate.Value > endDate.Value)
                {
                    throw new ArgumentException(
                        "StartDate cannot be later than EndDate.");
                }

                if (endDate.Value > today)
                {
                    throw new ArgumentException(
                        "EndDate cannot be in the future.");
                }
            }

            // ClientId değerini temizle.
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                clientId = clientId.Trim();
            }
            else
            {
                clientId = null;
            }

            // Günlük leaderboard.
            var daily = await GetFixedPeriodAsync(
                "daily",
                today.ToString("yyyy-MM-dd"),
                today,
                today,
                country,
                clientId,
                limit,
                cancellationToken);

            // Haftalık dönem: ISO standardına göre pazartesi başlangıcı.
            int daysSinceMonday =
                ((int)today.DayOfWeek + 6) % 7;

            var weeklyStartDate = today.AddDays(-daysSinceMonday);

            int isoYear = ISOWeek.GetYear(
                today.ToDateTime(TimeOnly.MinValue));

            int isoWeek = ISOWeek.GetWeekOfYear(
                today.ToDateTime(TimeOnly.MinValue));

            string weeklyPeriodKey = $"{isoYear}-W{isoWeek:D2}";

            var weekly = await GetFixedPeriodAsync(
                "weekly",
                weeklyPeriodKey,
                weeklyStartDate,
                today,
                country,
                clientId,
                limit,
                cancellationToken);

            // Aylık dönem: takvim ayının ilk günü.
            var monthlyStartDate = new DateOnly(
                today.Year,
                today.Month,
                1);

            var monthly = await GetFixedPeriodAsync(
                "monthly",
                today.ToString("yyyy-MM"),
                monthlyStartDate,
                today,
                country,
                clientId,
                limit,
                cancellationToken);

            // Tüm zamanlar dönemi.
            var allTime = await GetFixedPeriodAsync(
                "alltime",
                "all",
                null,
                today,
                country,
                clientId,
                limit,
                cancellationToken);

            // Özel tarih aralığı gönderildiyse oluştur.
            LeaderboardPeriodDto? customRange = null;

            if (startDate.HasValue && endDate.HasValue)
            {
                customRange = await GetCustomRangeAsync(
                    startDate.Value,
                    endDate.Value,
                    country,
                    clientId,
                    limit,
                    cancellationToken);
            }

            _logger.LogInformation(
                "All leaderboards retrieved successfully. Country: {Country}, Limit: {Limit}",
                country ?? "Global",
                limit);

            return new LeaderboardResponse
            {
                Daily = daily,
                Weekly = weekly,
                Monthly = monthly,
                AllTime = allTime,
                CustomRange = customRange
            };
        }

        // Günlük, haftalık, aylık ve tüm zamanlar leaderboard verisini getirir.
        // Redis çalışmıyorsa PostgreSQL üzerinden devam eder.
        private async Task<LeaderboardPeriodDto> GetFixedPeriodAsync(
            string period,
            string periodKey,
            DateOnly? startDate,
            DateOnly? endDate,
            string? country,
            string? clientId,
            int limit,
            CancellationToken cancellationToken)
        {
            string cacheKey =
                $"DailyChessPuzzle_leaderboard:{period}:{periodKey}:{country ?? "global"}";

            List<(string ClientId, int TotalScore, int SolvedCount)> topEntries;

            try
            {
                // Önce Redis kontrol edilir.
                bool cacheExists = await _cacheService.ExistsAsync(cacheKey);

                if (!cacheExists)
                {
                    await RebuildCacheAsync(
                        cacheKey,
                        period,
                        periodKey,
                        country,
                        cancellationToken);
                }

                topEntries = await _cacheService.GetTopAsync(
                    cacheKey,
                    limit);

                // Redis boşsa PostgreSQL üzerinden yeniden oluştur.
                if (topEntries.Count == 0)
                {
                    await RebuildCacheAsync(
                        cacheKey,
                        period,
                        periodKey,
                        country,
                        cancellationToken);

                    topEntries = await _cacheService.GetTopAsync(
                        cacheKey,
                        limit);
                }
            }
            catch (Exception ex) when (
                ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Redis leaderboard read failed. Falling back to PostgreSQL. CacheKey: {CacheKey}",
                    cacheKey);

                topEntries = await GetTopFromDatabaseAsync(
                    period,
                    periodKey,
                    country,
                    limit,
                    cancellationToken);
            }

            var rankings = CreateRankings(topEntries);

            LeaderboardEntryDto? currentUser = null;

            if (clientId != null)
            {
                try
                {
                    var userScore = await _cacheService.GetUserScoreAsync(
                        cacheKey,
                        clientId);

                    if (userScore != null)
                    {
                        var userRank = await _cacheService.GetRankAsync(
                            cacheKey,
                            clientId);

                        if (userRank.HasValue)
                        {
                            currentUser = new LeaderboardEntryDto
                            {
                                ClientId = clientId,
                                TotalScore = userScore.Value.TotalScore,
                                SolvedCount = userScore.Value.SolvedCount,
                                Rank = (int)userRank.Value
                            };
                        }
                    }
                }
                catch (Exception ex) when (
                    ex is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        ex,
                        "Redis current user lookup failed. ClientId: {ClientId}",
                        clientId);
                }

                // Redis'te kullanıcı bulunamadıysa PostgreSQL'den getir.
                if (currentUser == null)
                {
                    currentUser = await GetCurrentUserFromDatabaseAsync(
                        period,
                        periodKey,
                        country,
                        clientId,
                        cancellationToken);
                }
            }

            return new LeaderboardPeriodDto
            {
                Period = period,
                StartDate = startDate,
                EndDate = endDate,
                Rankings = rankings,
                CurrentUser = currentUser
            };
        }

        // Özel tarih aralığını günlük Redis verilerinden oluşturur.
        // Redis erişilemiyorsa PostgreSQL üzerinden hesaplar.
        private async Task<LeaderboardPeriodDto> GetCustomRangeAsync(
            DateOnly startDate,
            DateOnly endDate,
            string? country,
            string? clientId,
            int limit,
            CancellationToken cancellationToken)
        {
            List<(string ClientId, int TotalScore, int SolvedCount)> topEntries;

            LeaderboardEntryDto? currentUser = null;

            try
            {
                var dailyKeys = new List<string>();
                var versionKeys = new List<string>();

                // Başlangıç ve bitiş günleri dahil olmak üzere
                // her günün Redis anahtarını oluştur.
                for (
                    var date = startDate;
                    date <= endDate;
                    date = date.AddDays(1))
                {
                    string dailyKey =
                        $"DailyChessPuzzle_leaderboard:daily:{date:yyyy-MM-dd}:{country ?? "global"}";

                    dailyKeys.Add(dailyKey);
                    versionKeys.Add($"{dailyKey}:version");
                }

                // Günlük versiyonları okuyarak özel aralık önbelleğinin
                // güncelliğini belirle.
                var versions = new List<long>();

                foreach (var versionKey in versionKeys)
                {
                    long version = await _cacheService.GetVersionAsync(
                        versionKey);

                    versions.Add(version);
                }

                // Versiyon listesinden kısa ve sabit bir parmak izi üret.
                string versionData = string.Join(",", versions);

                string versionFingerprint = Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(versionData)
                    )
                )[..16];

                // Günlük veriler değişirse yeni bir özel aralık anahtarı oluşur.
                string rangeKey =
                    $"DailyChessPuzzle_leaderboard:custom:{startDate:yyyy-MM-dd}:{endDate:yyyy-MM-dd}:{country ?? "global"}:{versionFingerprint}";

                // Özel tarih aralığı cache kaydı yoksa oluştur.
                bool cacheExists = await _cacheService.ExistsAsync(rangeKey);

                if (!cacheExists)
                {
                    await _cacheService.RebuildRangeAsync(
                        rangeKey,
                        dailyKeys,
                        CustomRangeCacheDuration);
                }

                // İlk N kullanıcıyı Redis'ten getir.
                topEntries = await _cacheService.GetTopAsync(
                    rangeKey,
                    limit);

                // Kullanıcının kendi sıralamasını Redis'ten getir.
                if (clientId != null)
                {
                    var userScore = await _cacheService.GetUserScoreAsync(
                        rangeKey,
                        clientId);

                    if (userScore != null)
                    {
                        var userRank = await _cacheService.GetRankAsync(
                            rangeKey,
                            clientId);

                        if (userRank.HasValue)
                        {
                            currentUser = new LeaderboardEntryDto
                            {
                                ClientId = clientId,
                                TotalScore = userScore.Value.TotalScore,
                                SolvedCount = userScore.Value.SolvedCount,
                                Rank = (int)userRank.Value
                            };
                        }
                    }
                }
            }
            catch (Exception ex) when (
                ex is not OperationCanceledException)
            {
                // Redis erişilemiyorsa PostgreSQL'e geç.
                _logger.LogWarning(
                    ex,
                    "Custom range Redis read failed. Falling back to PostgreSQL. StartDate: {StartDate}, EndDate: {EndDate}",
                    startDate,
                    endDate);

                topEntries = await GetCustomRangeTopFromDatabaseAsync(
                    startDate,
                    endDate,
                    country,
                    limit,
                    cancellationToken);

                if (clientId != null)
                {
                    currentUser = await GetCustomRangeCurrentUserFromDatabaseAsync(
                        startDate,
                        endDate,
                        country,
                        clientId,
                        cancellationToken);
                }
            }

            // Redis'te kullanıcı bulunamadıysa PostgreSQL'den getir.
            if (clientId != null && currentUser == null)
            {
                currentUser = await GetCustomRangeCurrentUserFromDatabaseAsync(
                    startDate,
                    endDate,
                    country,
                    clientId,
                    cancellationToken);
            }

            return new LeaderboardPeriodDto
            {
                Period = "custom",
                StartDate = startDate,
                EndDate = endDate,
                Rankings = CreateRankings(topEntries),
                CurrentUser = currentUser
            };
        }

        // PostgreSQL üzerinden özel tarih aralığının ilk N kullanıcısını getirir.
        private async Task<List<(string ClientId, int TotalScore, int SolvedCount)>>
            GetCustomRangeTopFromDatabaseAsync(
                DateOnly startDate,
                DateOnly endDate,
                string? country,
                int limit,
                CancellationToken cancellationToken)
        {
            var query = _context.PuzzleAttempts
                .AsNoTracking()
                .Where(x =>
                    x.PlayedOn >= startDate &&
                    x.PlayedOn <= endDate);

            // Ülke filtresi gönderilmişse uygula.
            if (country != null)
            {
                query = query.Where(x => x.CountryCode == country);
            }

            var entries = await query
                .GroupBy(x => x.ClientId)
                .Select(group => new
                {
                    ClientId = group.Key,
                    TotalScore = group.Sum(x => x.Score),
                    SolvedCount = group.Count()
                })
                .OrderByDescending(x => x.TotalScore)
                .ThenByDescending(x => x.SolvedCount)
                .ThenBy(x => x.ClientId)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return entries
                .Select(x => (
                    x.ClientId,
                    x.TotalScore,
                    x.SolvedCount))
                .ToList();
        }

        // PostgreSQL üzerinden kullanıcının özel tarih aralığındaki
        // puanını ve rekabetçi sıralamasını hesaplar.
        private async Task<LeaderboardEntryDto?> GetCustomRangeCurrentUserFromDatabaseAsync(
            DateOnly startDate,
            DateOnly endDate,
            string? country,
            string clientId,
            CancellationToken cancellationToken)
        {
            var query = _context.PuzzleAttempts
                .AsNoTracking()
                .Where(x =>
                    x.PlayedOn >= startDate &&
                    x.PlayedOn <= endDate);

            // Ülke filtresi gönderilmişse uygula.
            if (country != null)
            {
                query = query.Where(x => x.CountryCode == country);
            }

            // Kullanıcının toplam puanını ve çözdüğü bulmaca sayısını hesapla.
            var userEntry = await query
                .Where(x => x.ClientId == clientId)
                .GroupBy(x => x.ClientId)
                .Select(group => new
                {
                    ClientId = group.Key,
                    TotalScore = group.Sum(x => x.Score),
                    SolvedCount = group.Count()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (userEntry == null)
            {
                return null;
            }

            // Daha yüksek puan veya eşit puanda daha fazla çözüm
            // yapan kullanıcılar önde kabul edilir.
            var usersAhead = await query
                .GroupBy(x => x.ClientId)
                .Select(group => new
                {
                    TotalScore = group.Sum(x => x.Score),
                    SolvedCount = group.Count()
                })
                .CountAsync(x =>
                    x.TotalScore > userEntry.TotalScore ||
                    (
                        x.TotalScore == userEntry.TotalScore &&
                        x.SolvedCount > userEntry.SolvedCount
                    ),
                    cancellationToken);

            return new LeaderboardEntryDto
            {
                ClientId = userEntry.ClientId,
                TotalScore = userEntry.TotalScore,
                SolvedCount = userEntry.SolvedCount,
                Rank = usersAhead + 1
            };
        }

        // Redis'ten gelen kayıtları DTO'ya dönüştürür.
        // Aynı puan ve çözüm sayısına sahip kullanıcılar aynı dereceyi alır.
        private static List<LeaderboardEntryDto> CreateRankings(
            List<(string ClientId, int TotalScore, int SolvedCount)> entries)
        {
            var rankings = new List<LeaderboardEntryDto>();

            int currentRank = 0;
            int previousScore = -1;
            int previousSolvedCount = -1;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (i == 0 ||
                    entry.TotalScore != previousScore ||
                    entry.SolvedCount != previousSolvedCount)
                {
                    currentRank = i + 1;
                }

                rankings.Add(new LeaderboardEntryDto
                {
                    ClientId = entry.ClientId,
                    TotalScore = entry.TotalScore,
                    SolvedCount = entry.SolvedCount,
                    Rank = currentRank
                });

                previousScore = entry.TotalScore;
                previousSolvedCount = entry.SolvedCount;
            }

            return rankings;
        }

        // Ülke kodunu doğrular ve standart biçime dönüştürür.
        private static string? ValidateCountry(string? country)
        {
            if (string.IsNullOrWhiteSpace(country))
            {
                return null;
            }

            country = country.Trim().ToUpperInvariant();

            if (country.Length != 2)
            {
                throw new ArgumentException(
                    "Country must be a valid ISO 3166-1 alpha-2 country code.");
            }

            try
            {
                _ = new RegionInfo(country);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException(
                    "Country must be a valid ISO 3166-1 alpha-2 country code.");
            }

            return country;
        }

        // PostgreSQL'den istenen sayıda kullanıcıyı getirir.
        private async Task<List<(string ClientId, int TotalScore, int SolvedCount)>>
            GetTopFromDatabaseAsync(
                string period,
                string periodKey,
                string? country,
                int limit,
                CancellationToken cancellationToken)
        {
            var entries = await _context.LeaderboardScores
                .AsNoTracking()
                .Where(x =>
                    x.Period == period &&
                    x.PeriodKey == periodKey &&
                    x.CountryCode == country)
                .OrderByDescending(x => x.TotalScore)
                .ThenByDescending(x => x.SolvedCount)
                .ThenBy(x => x.ClientId)
                .Take(limit)
                .Select(x => new
                {
                    x.ClientId,
                    x.TotalScore,
                    x.SolvedCount
                })
                .ToListAsync(cancellationToken);

            return entries
                .Select(x => (
                    x.ClientId,
                    x.TotalScore,
                    x.SolvedCount))
                .ToList();
        }

        // PostgreSQL'deki leaderboard kayıtlarını Redis'e yeniden yükler.
        private async Task RebuildCacheAsync(
            string cacheKey,
            string period,
            string periodKey,
            string? country,
            CancellationToken cancellationToken)
        {
            var entries = await _context.LeaderboardScores
                .AsNoTracking()
                .Where(x =>
                    x.Period == period &&
                    x.PeriodKey == periodKey &&
                    x.CountryCode == country)
                .Select(x => new
                {
                    x.ClientId,
                    x.TotalScore,
                    x.SolvedCount
                })
                .ToListAsync(cancellationToken);

            var cacheEntries = entries
                .Select(x => (
                    x.ClientId,
                    x.TotalScore,
                    x.SolvedCount))
                .ToList();

            await _cacheService.RebuildAsync(
                cacheKey,
                cacheEntries);

            _logger.LogInformation(
                "Leaderboard Redis cache rebuilt. CacheKey: {CacheKey}, UserCount: {UserCount}",
                cacheKey,
                cacheEntries.Count);
        }

        // Kullanıcının kendi sırasını PostgreSQL'den hesaplar.
        private async Task<LeaderboardEntryDto?> GetCurrentUserFromDatabaseAsync(
            string period,
            string periodKey,
            string? country,
            string clientId,
            CancellationToken cancellationToken)
        {
            var query = _context.LeaderboardScores
                .AsNoTracking()
                .Where(x =>
                    x.Period == period &&
                    x.PeriodKey == periodKey &&
                    x.CountryCode == country);

            var userEntry = await query
                .Where(x => x.ClientId == clientId)
                .Select(x => new
                {
                    x.ClientId,
                    x.TotalScore,
                    x.SolvedCount
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (userEntry == null)
            {
                return null;
            }

            // Yalnızca puanı veya çözüm sayısı daha yüksek olanlar
            // kullanıcının önünde kabul edilir.
            var usersAhead = await query
                .CountAsync(x =>
                    x.TotalScore > userEntry.TotalScore ||
                    (
                        x.TotalScore == userEntry.TotalScore &&
                        x.SolvedCount > userEntry.SolvedCount
                    ),
                    cancellationToken);

            return new LeaderboardEntryDto
            {
                ClientId = userEntry.ClientId,
                TotalScore = userEntry.TotalScore,
                SolvedCount = userEntry.SolvedCount,
                Rank = usersAhead + 1
            };
        }
    }
}
