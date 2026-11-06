
using DailyChessPuzzleApi.DTOs;
using DailyChessPuzzleApi.Services.Interfaces;
using DailyChessPuzzleApi.Responses;
using Microsoft.AspNetCore.Mvc;

namespace DailyChessPuzzleApi.Controllers
{
    [ApiController]
    [Route("api/puzzle")]
    public class PuzzleController : ControllerBase
    {
        private readonly IPuzzleService _puzzleService;
        private readonly ILogger<PuzzleController> _logger;

        public PuzzleController(
            IPuzzleService puzzleService,
            ILogger<PuzzleController> logger)
        {
            _puzzleService = puzzleService;
            _logger = logger;
        }

        // Günün bulmacasını getirir.
        [HttpGet("today")]
        public async Task<IActionResult> GetToday(
            [FromQuery] string clientId,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "GET today's puzzle request received."
            );

            if (string.IsNullOrWhiteSpace(clientId))
            {
                _logger.LogWarning(
                    "GET today's puzzle request rejected. ClientId is missing."
                );

                return BadRequest(ResponseFactory.Failure<object>(
                    HttpContext,
                    "ClientId is required.",
                    400,
                    "CLIENT_ID_REQUIRED"
                ));
            }

            var puzzle = await _puzzleService.GetTodayAsync(
                clientId,
                cancellationToken
            );

            if (puzzle == null)
            {
                return NotFound(ResponseFactory.Failure<object>(
                    HttpContext,
                    "Today's puzzle was not found.",
                    404,
                    "PUZZLE_NOT_FOUND"
                ));
            }

            return Ok(ResponseFactory.Success(
                HttpContext,
                "Today's puzzle retrieved successfully.",
                puzzle
            ));
        }

        // Bulmaca sonucunu gönderir.
        [HttpPost("submit")]
        public async Task<IActionResult> Submit(
            [FromBody] PuzzleSubmitRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "POST puzzle submission request received. PuzzleId: {PuzzleId}",
                request.PuzzleId
            );

            if (string.IsNullOrWhiteSpace(request.ClientId))
            {
                _logger.LogWarning(
                    "Puzzle submission rejected. ClientId is missing."
                );

                return BadRequest(ResponseFactory.Failure<object>(
                    HttpContext,
                    "ClientId is required.",
                    400,
                    "CLIENT_ID_REQUIRED"
                ));
            }

            if (request.ClientId.Length > 100)
            {
                _logger.LogWarning(
                    "Puzzle submission rejected. ClientId exceeds maximum length."
                );

                return BadRequest(ResponseFactory.Failure<object>(
                    HttpContext,
                    "ClientId is too long.",
                    400,
                    "CLIENT_ID_TOO_LONG"
                ));
            }

            if (request.CountryCode != null &&
                request.CountryCode.Length != 2)
            {
                _logger.LogWarning(
                    "Puzzle submission rejected. Invalid CountryCode length."
                );

                return BadRequest(ResponseFactory.Failure<object>(
                    HttpContext,
                    "CountryCode must contain 2 characters.",
                    400,
                    "INVALID_COUNTRY_CODE"
                ));
            }

            var response = await _puzzleService.SubmitAsync(
                request,
                cancellationToken
            );

            return Ok(ResponseFactory.Success(
                HttpContext,
                "Puzzle submission completed successfully.",
                response
            ));
        }
    }
}
