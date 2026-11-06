
using DailyChessPuzzleApi.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DailyChessPuzzleApi.Controllers
{
    [ApiController]
    [Route("api/leaderboard")]
    public class LeaderboardController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboardService;

        public LeaderboardController(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        [HttpGet]
        public async Task<IActionResult> GetLeaderboard(
            [FromQuery] string period = "daily",
            [FromQuery] string? country = null,
            [FromQuery] string? clientId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _leaderboardService.GetLeaderboardAsync(
                    period,
                    country,
                    clientId,
                    cancellationToken
                );

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
