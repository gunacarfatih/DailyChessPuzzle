namespace DailyChessPuzzleApi.DTOs
{
    public class LeaderboardEntryDto
    {
        public int Rank { get; set; }

        public string ClientId { get; set; } = string.Empty;

        public int TotalScore { get; set; }

        public int SolvedCount { get; set; }
    }
}