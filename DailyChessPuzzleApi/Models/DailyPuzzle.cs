namespace DailyChessPuzzleApi.Models
{
    public class DailyPuzzle
    {
        public int Id { get; set; }

        public int PuzzleId { get; set; }

        public Puzzle Puzzle { get; set; } = null!;

        public DateOnly PuzzleDate { get; set; }
    }
}