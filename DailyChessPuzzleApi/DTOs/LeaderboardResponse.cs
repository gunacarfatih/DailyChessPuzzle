
namespace DailyChessPuzzleApi.DTOs
{
    public class LeaderboardResponse
    {
        // Günlük sıralama.
        public LeaderboardPeriodDto Daily { get; set; } = new();

        // Haftalık sıralama.
        public LeaderboardPeriodDto Weekly { get; set; } = new();

        // Aylık sıralama.
        public LeaderboardPeriodDto Monthly { get; set; } = new();

        // Tüm zamanların sıralaması.
        public LeaderboardPeriodDto AllTime { get; set; } = new();

        // İsteğe bağlı özel tarih aralığı sıralaması.
        public LeaderboardPeriodDto? CustomRange { get; set; }
    }
}
