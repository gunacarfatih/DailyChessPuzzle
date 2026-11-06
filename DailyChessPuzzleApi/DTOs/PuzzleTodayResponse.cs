namespace DailyChessPuzzleApi.DTOs
{
    public class PuzzleTodayResponse
    {
        public int Id { get; set; }

        public string Fen { get; set; } = string.Empty;

        public string Moves { get; set; } = string.Empty;

        public string Difficulty { get; set; } = string.Empty;

        public bool IsSolved { get; set; }
    }
}