namespace DailyChessPuzzleApi.Models
{
    public class PuzzleAttempt
    {
        public int Id { get; set; }

        public int PuzzleId { get; set; }

        public Puzzle? Puzzle { get; set; }

        public string ClientId { get; set; } = string.Empty;

        public int Seconds { get; set; }

        public int WrongMoves { get; set; }

        public bool UsedHint { get; set; }

        public int Score { get; set; }

        public string? CountryCode { get; set; }

        public DateOnly PlayedOn { get; set; }

        public DateTime SubmittedAtUtc { get; set; }
    }
}