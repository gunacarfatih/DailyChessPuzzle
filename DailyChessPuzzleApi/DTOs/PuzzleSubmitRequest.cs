using System.ComponentModel.DataAnnotations;

namespace DailyChessPuzzleApi.DTOs
{
    public class PuzzleSubmitRequest
    {
        [Required]
        public string ClientId { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int PuzzleId { get; set; }

        [Range(0, int.MaxValue)]
        public int Seconds { get; set; }

        [Range(0, int.MaxValue)]
        public int WrongMoves { get; set; }

        public bool UsedHint { get; set; }

        [StringLength(2, MinimumLength = 2)]
        public string? CountryCode { get; set; }
    }
}