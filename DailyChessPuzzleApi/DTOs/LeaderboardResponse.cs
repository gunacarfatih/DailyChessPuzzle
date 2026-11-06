namespace DailyChessPuzzleApi.DTOs
{
    public class LeaderboardResponse
    {
        public string Period { get; set; } = string.Empty;

        public string? Country { get; set; }

        public List<LeaderboardEntryDto> Rankings { get; set; } = new();

        public LeaderboardEntryDto? CurrentUser { get; set; }
    }
}