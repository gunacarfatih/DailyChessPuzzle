
using DailyChessPuzzleApi.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DailyChessPuzzleApi.Controllers
{
    [ApiController]
    [Route("api/leaderboard")]
    public class LeaderboardController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboardService;
        private readonly ILogger<LeaderboardController> _logger;

        public LeaderboardController(
            ILeaderboardService leaderboardService,
            ILogger<LeaderboardController> logger)
        {
            _leaderboardService = leaderboardService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetLeaderboard(
            [FromQuery] string period = "daily",
            [FromQuery] string? country = null,
            [FromQuery] string? clientId = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "GET leaderboard request received. Period: {Period}, Country: {Country}",
                period,
                country ?? "Global"
            );

            var response = await _leaderboardService.GetLeaderboardAsync(
                period,
                country,
                clientId,
                cancellationToken
            );

            return Ok(response);
        }
    }
}
