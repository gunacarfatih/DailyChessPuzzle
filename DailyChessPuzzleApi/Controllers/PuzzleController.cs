
using DailyChessPuzzleApi.DTOs;
using DailyChessPuzzleApi.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DailyChessPuzzleApi.Controllers
{
    [ApiController]
    [Route("api/puzzle")]
    public class PuzzleController : ControllerBase
    {
        private readonly IPuzzleService _puzzleService;

        public PuzzleController(IPuzzleService puzzleService)
        {
            _puzzleService = puzzleService;
        }

        // Günün bulmacasını getirir.
        [HttpGet("today")]
        public async Task<IActionResult> GetToday(
            [FromQuery] string clientId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                return BadRequest("ClientId is required.");

            var puzzle = await _puzzleService.GetTodayAsync(
                clientId,
                cancellationToken
            );

            if (puzzle == null)
                return NotFound("Today's puzzle was not found.");

            return Ok(puzzle);
        }

        // Bulmaca sonucunu gönderir.
        [HttpPost("submit")]
        public async Task<IActionResult> Submit(
            [FromBody] PuzzleSubmitRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ClientId))
                return BadRequest("ClientId is required.");

            if (request.ClientId.Length > 100)
                return BadRequest("ClientId is too long.");

            if (request.CountryCode != null &&
                request.CountryCode.Length != 2)
            {
                return BadRequest(
                    "CountryCode must contain 2 characters."
                );
            }

            try
            {
                var response = await _puzzleService.SubmitAsync(
                    request,
                    cancellationToken
                );

                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Today's puzzle was not found.");
            }
            catch (InvalidOperationException)
            {
                return Conflict(
                    "You have already submitted this puzzle."
                );
            }
        }
    }
}
