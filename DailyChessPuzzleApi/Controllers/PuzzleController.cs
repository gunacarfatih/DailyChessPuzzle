using DailyChessPuzzleApi.Data;
using DailyChessPuzzleApi.DTOs;
using DailyChessPuzzleApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyChessPuzzleApi.Controllers
{
    [ApiController]
    [Route("api/puzzle")]
    public class PuzzleController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PuzzleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/puzzle/today?clientId=...
        [HttpGet("today")]
        public async Task<IActionResult> GetToday(
            [FromQuery] string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return BadRequest("ClientId is required.");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var puzzle = await _context.Puzzles
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.PuzzleDate == today && p.IsActive);

            if (puzzle == null)
            {
                return NotFound("Today's puzzle was not found.");
            }

            var isSolved = await _context.PuzzleAttempts
                .AnyAsync(a =>
                    a.PuzzleId == puzzle.Id &&
                    a.ClientId == clientId);

            var response = new PuzzleTodayResponse
            {
                Id = puzzle.Id,
                Fen = puzzle.Fen,
                Moves = puzzle.Moves,
                Difficulty = puzzle.Difficulty,
                IsSolved = isSolved
            };

            return Ok(response);
        }

        // POST: api/puzzle/submit
        [HttpPost("submit")]
        public async Task<IActionResult> Submit(
            [FromBody] PuzzleSubmitRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ClientId))
            {
                return BadRequest("ClientId is required.");
            }

            if (request.ClientId.Length > 100)
            {
                return BadRequest("ClientId is too long.");
            }

            if (request.CountryCode != null &&
                request.CountryCode.Length != 2)
            {
                return BadRequest("CountryCode must contain 2 characters.");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var puzzle = await _context.Puzzles
                .FirstOrDefaultAsync(p =>
                    p.Id == request.PuzzleId &&
                    p.PuzzleDate == today &&
                    p.IsActive);

            if (puzzle == null)
            {
                return NotFound("Today's puzzle was not found.");
            }

            var alreadySolved = await _context.PuzzleAttempts
                .AnyAsync(a =>
                    a.PuzzleId == request.PuzzleId &&
                    a.ClientId == request.ClientId);

            if (alreadySolved)
            {
                return Conflict("You have already submitted this puzzle.");
            }

            // Score calculation
            int timePenalty = request.Seconds * 2;

            int wrongMovePenalty = request.WrongMoves * 150;

            int hintPenalty = request.UsedHint ? 250 : 0;

            int score = Math.Max(
                0,
                1000 - timePenalty - wrongMovePenalty - hintPenalty
            );

            var attempt = new PuzzleAttempt
            {
                PuzzleId = puzzle.Id,
                ClientId = request.ClientId,
                Seconds = request.Seconds,
                WrongMoves = request.WrongMoves,
                UsedHint = request.UsedHint,
                Score = score,
                CountryCode = request.CountryCode?.ToUpperInvariant(),
                PlayedOn = today,
                SubmittedAtUtc = DateTime.UtcNow
            };

            await _context.PuzzleAttempts.AddAsync(attempt);

            await _context.SaveChangesAsync();

            var response = new PuzzleSubmitResponse
            {
                Score = score,
                Seconds = request.Seconds,
                WrongMoves = request.WrongMoves,
                UsedHint = request.UsedHint,
                Message = "Puzzle result saved successfully."
            };

            return Ok(response);
        }
    }
}