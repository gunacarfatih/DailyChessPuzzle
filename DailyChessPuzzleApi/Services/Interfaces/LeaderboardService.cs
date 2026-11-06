
using DailyChessPuzzleApi.Data;
using DailyChessPuzzleApi.DTOs;
using DailyChessPuzzleApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyChessPuzzleApi.Services
{
    public class LeaderboardService : ILeaderboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LeaderboardService> _logger;

        public LeaderboardService(
            ApplicationDbContext context,
            ILogger<LeaderboardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<LeaderboardResponse> GetLeaderboardAsync(
            string period,
            string? country,
            string? clientId,
            CancellationToken cancellationToken = default)
        {
            period = (period ?? "daily").Trim().ToLowerInvariant();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            DateOnly startDate;

            switch (period)
            {
                case "daily":
                    startDate = today;
                    break;

                case "weekly":
                    startDate = today.AddDays(-6);
                    break;

                case "monthly":
                    startDate = new DateOnly(today.Year, today.Month, 1);
                    break;

                default:
                    _logger.LogWarning(
                        "Invalid leaderboard period requested: {Period}",
                        period
                    );

                    throw new ArgumentException(
                        "Period must be daily, weekly or monthly."
                    );
            }

            if (!string.IsNullOrWhiteSpace(country))
            {
                country = country.Trim();

                if (country.Length != 2)
                {
                    _logger.LogWarning(
                        "Invalid country code provided for leaderboard."
                    );

                    throw new ArgumentException(
                        "Country must be a 2-character ISO country code."
                    );
                }

                country = country.ToUpperInvariant();
            }
            else
            {
                country = null;
            }

            try
            {
                var query = _context.PuzzleAttempts
                    .AsNoTracking()
                    .Where(a => a.PlayedOn >= startDate &&
                                a.PlayedOn <= today);

                if (country != null)
                    query = query.Where(a => a.CountryCode == country);

                var groupedQuery = query
                    .GroupBy(a => a.ClientId)
                    .Select(group => new LeaderboardEntryDto
                    {
                        ClientId = group.Key,
                        TotalScore = group.Sum(a => a.Score),
                        SolvedCount = group.Count()
                    });

                var rankings = await groupedQuery
                    .OrderByDescending(x => x.TotalScore)
                    .ThenByDescending(x => x.SolvedCount)
                    .ThenBy(x => x.ClientId)
                    .Take(50)
                    .ToListAsync(cancellationToken);

                for (int i = 0; i < rankings.Count; i++)
                {
                    if (i == 0)
                        rankings[i].Rank = 1;
                    else if (
                        rankings[i].TotalScore == rankings[i - 1].TotalScore &&
                        rankings[i].SolvedCount == rankings[i - 1].SolvedCount)
                        rankings[i].Rank = rankings[i - 1].Rank;
                    else
                        rankings[i].Rank = i + 1;
                }

                LeaderboardEntryDto? currentUser = null;

                if (!string.IsNullOrWhiteSpace(clientId))
                {
                    clientId = clientId.Trim();

                    var userScore = await groupedQuery
                        .FirstOrDefaultAsync(
                            x => x.ClientId == clientId,
                            cancellationToken
                        );

                    if (userScore != null)
                    {
                        var usersAhead = await groupedQuery.CountAsync(
                            x => x.TotalScore > userScore.TotalScore ||
                                 (x.TotalScore == userScore.TotalScore &&
                                  x.SolvedCount > userScore.SolvedCount),
                            cancellationToken
                        );

                        userScore.Rank = usersAhead + 1;
                        currentUser = userScore;
                    }
                }

                _logger.LogInformation(
                    "Leaderboard retrieved successfully. Period: {Period}, Country: {Country}, ResultCount: {ResultCount}",
                    period,
                    country ?? "Global",
                    rankings.Count
                );

                return new LeaderboardResponse
                {
                    Period = period,
                    Country = country,
                    Rankings = rankings,
                    CurrentUser = currentUser
                };
            }
            catch (OperationCanceledException)
            {
                // İptal edilen istekleri beklenmeyen hata olarak loglamıyoruz.
                throw;
            }
            
        }
    }
}
