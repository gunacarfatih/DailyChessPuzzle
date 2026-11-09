namespace DailyChessPuzzleApi.Settings
{
    public class LeaderboardSettings
    {
        public int DefaultLimit { get; set; } = 50;

        public int MinLimit { get; set; } = 1;

        public int MaxLimit { get; set; } = 100;
    }
}