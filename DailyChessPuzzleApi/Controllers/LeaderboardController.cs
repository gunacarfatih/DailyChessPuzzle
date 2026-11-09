
using DailyChessPuzzleApi.Services.Interfaces;
using DailyChessPuzzleApi.Responses;
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

        // Günlük, haftalık, aylık, tüm zamanlar
        // ve isteğe bağlı özel tarih aralığı leaderboard verilerini getirir.
        [HttpGet]
        public async Task<IActionResult> GetLeaderboard(
            [FromQuery] string? country = null,
            [FromQuery] string? clientId = null,
            [FromQuery] int limit = 0,
            [FromQuery] DateOnly? startDate = null,
            [FromQuery] DateOnly? endDate = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "GET leaderboard request received. Country: {Country}, ClientId: {ClientId}, Limit: {Limit}, StartDate: {StartDate}, EndDate: {EndDate}",
                country ?? "Global",
                clientId,
                limit,
                startDate,
                endDate
            );

            // Liderlik tablosu servisinden veriler alınır.
            var response = await _leaderboardService.GetLeaderboardAsync(
                country,
                clientId,
                limit,
                startDate,
                endDate,
                cancellationToken
            );

            // Başarılı cevap ResponseFactory üzerinden oluşturulur.
            var baseResponse = ResponseFactory.Success(
                HttpContext,
                "Leaderboard retrieved successfully.",
                response
            );

            return Ok(baseResponse);
        }
    }
}
