namespace DailyChessPuzzleApi.Models
{
    public class Puzzle
    {
        public int Id { get; set; }

        public string LichessPuzzleId { get; set; } = string.Empty;

        public string Fen { get; set; } = string.Empty;

        public string Moves { get; set; } = string.Empty;

        public int Rating { get; set; }

        public int Popularity { get; set; }

        public string Themes { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public ICollection<DailyPuzzle> DailyPuzzles { get; set; }
            = new List<DailyPuzzle>();
    }
}