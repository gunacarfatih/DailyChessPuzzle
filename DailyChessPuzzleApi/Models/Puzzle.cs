namespace DailyChessPuzzleApi.Models
{
    public class Puzzle
    {
        public int Id { get; set; }

        public string Fen { get; set; } = string.Empty;

        public string Moves { get; set; } = string.Empty;

        public string Difficulty { get; set; } = string.Empty;

        public DateOnly PuzzleDate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}