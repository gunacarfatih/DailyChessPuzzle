using DailyChessPuzzleApi.Data;
using DailyChessPuzzleApi.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyChessPuzzleApi.Controllers
{
    [ApiController]
    [Route("api/leaderboard")]
    public class LeaderboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LeaderboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/leaderboard?period=daily&country=TR
        [HttpGet]
        public async Task<IActionResult> GetLeaderboard(
            [FromQuery] string period = "daily",
            [FromQuery] string? country = null,
            [FromQuery] string? clientId = null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            DateOnly startDate;

            switch (period.ToLowerInvariant())
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
                    return BadRequest(
                        "Period must be daily, weekly or monthly."
                    );
            }

            if (country != null && country.Length != 2)
            {
                return BadRequest(
                    "Country must be a 2-character ISO country code."
                );
            }

            var query = _context.PuzzleAttempts
                .AsNoTracking()
                .Where(a =>
                    a.PlayedOn >= startDate &&
                    a.PlayedOn <= today);

            if (!string.IsNullOrWhiteSpace(country))
            {
                country = country.ToUpperInvariant();

                query = query.Where(a =>
                    a.CountryCode == country);
            }

            var rankings = await query
                .GroupBy(a => a.ClientId)
                .Select(group => new LeaderboardEntryDto
                {
                    ClientId = group.Key,
                    TotalScore = group.Sum(a => a.Score),
                    SolvedCount = group.Count()
                })
                .OrderByDescending(x => x.TotalScore)
                .ThenByDescending(x => x.SolvedCount)
                .Take(50)
                .ToListAsync();

            for (int i = 0; i < rankings.Count; i++)
            {
                rankings[i].Rank = i + 1;
            }

            LeaderboardEntryDto? currentUser = null;

            if (!string.IsNullOrWhiteSpace(clientId))
            {
                currentUser = rankings.FirstOrDefault(x =>
                    x.ClientId == clientId);
            }

            var response = new LeaderboardResponse
            {
                Period = period.ToLowerInvariant(),
                Country = country,
                Rankings = rankings,
                CurrentUser = currentUser
            };

            return Ok(response);
        }
    }
}