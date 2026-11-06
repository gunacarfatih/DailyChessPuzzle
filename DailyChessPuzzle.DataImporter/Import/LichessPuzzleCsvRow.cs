namespace DailyChessPuzzle.DataImporter.Import
{
    public class LichessPuzzleCsvRow
    {
        public string PuzzleId { get; set; } = string.Empty;

        public string FEN { get; set; } = string.Empty;

        public string Moves { get; set; } = string.Empty;

        public int Rating { get; set; }

        public int Popularity { get; set; }

        public string Themes { get; set; } = string.Empty;
    }
}