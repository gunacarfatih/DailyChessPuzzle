
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

            country = ValidateCountry(country);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

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

            clientId = string.IsNullOrWhiteSpace(clientId)
                ? null
                : clientId.Trim();

            var daily = await GetFixedPeriodAsync(
                "daily",
                today.ToString("yyyy-MM-dd"),
                today,
                today,
                country,
                clientId,
                limit,
                cancellationToken);

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

            var allTime = await GetFixedPeriodAsync(
                "alltime",
                "all",
                null,
                today,
                country,
                clientId,
                limit,
                cancellationToken);

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

                for (var date = startDate;
                     date <= endDate;
                     date = date.AddDays(1))
                {
                    string dailyKey =
                        $"DailyChessPuzzle_leaderboard:daily:{date:yyyy-MM-dd}:{country ?? "global"}";

                    string versionKey = $"{dailyKey}:version";

                    await EnsureDailyCacheReadyAsync(
                        dailyKey,
                        versionKey,
                        date,
                        country,
                        cancellationToken);

                    dailyKeys.Add(dailyKey);
                    versionKeys.Add(versionKey);
                }

                var versions = new List<long>();

                foreach (var versionKey in versionKeys)
                {
                    versions.Add(
                        await _cacheService.GetVersionAsync(versionKey));
                }

                string versionData = string.Join(",", versions);

                string versionFingerprint = Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(versionData)))
                    [..16];

                string rangeKey =
                    $"DailyChessPuzzle_leaderboard:custom:{startDate:yyyy-MM-dd}:{endDate:yyyy-MM-dd}:{country ?? "global"}:{versionFingerprint}";

                bool cacheExists = await _cacheService.ExistsAsync(rangeKey);

                if (!cacheExists)
                {
                    await _cacheService.RebuildRangeAsync(
                        rangeKey,
                        dailyKeys,
                        CustomRangeCacheDuration);
                }

                topEntries = await _cacheService.GetTopAsync(
                    rangeKey,
                    limit);

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
                _logger.LogWarning(
                    ex,
                    "Custom range Redis operation failed. Falling back to PostgreSQL. StartDate: {StartDate}, EndDate: {EndDate}",
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

        private async Task EnsureDailyCacheReadyAsync(
            string dailyKey,
            string versionKey,
            DateOnly date,
            string? country,
            CancellationToken cancellationToken)
        {
            bool isReady = await _cacheService.IsDailyCacheReadyAsync(
                dailyKey);

            if (isReady)
            {
                return;
            }

            var entries = await _context.LeaderboardScores
                .AsNoTracking()
                .Where(x =>
                    x.Period == "daily" &&
                    x.PeriodKey == date.ToString("yyyy-MM-dd") &&
                    x.CountryCode == country)
                .Select(x => new
                {
                    x.ClientId,
                    x.TotalScore,
                    x.SolvedCount,
                    x.Revision
                })
                .ToListAsync(cancellationToken);

            var cacheEntries = entries
                .Select(x => (
                    x.ClientId,
                    x.TotalScore,
                    x.SolvedCount,
                    x.Revision))
                .ToList();

            await _cacheService.RebuildDailyAsync(
                dailyKey,
                cacheEntries);

            await _cacheService.IncrementVersionAsync(versionKey);

            _logger.LogInformation(
                "Daily leaderboard cache initialized. Date: {Date}, Country: {Country}, UserCount: {UserCount}",
                date,
                country ?? "Global",
                cacheEntries.Count);
        }

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

            if (country != null)
            {
                query = query.Where(x => x.CountryCode == country);
            }

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
                    x.SolvedCount,
                    x.Revision
                })
                .ToListAsync(cancellationToken);

            var cacheEntries = entries
                .Select(x => (
                    x.ClientId,
                    x.TotalScore,
                    x.SolvedCount,
                    x.Revision))
                .ToList();

            await _cacheService.RebuildAsync(
                cacheKey,
                cacheEntries);

            _logger.LogInformation(
                "Leaderboard Redis cache rebuilt. CacheKey: {CacheKey}, UserCount: {UserCount}",
                cacheKey,
                cacheEntries.Count);
        }

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
