
namespace DailyChessPuzzleApi.Models
{
    public class LeaderboardCacheOutbox
    {
        public int Id { get; set; }

        // Redis cache anahtarı.
        public string CacheKey { get; set; } = string.Empty;

        // Puanı güncellenecek kullanıcı.
        public string ClientId { get; set; } = string.Empty;

        // Güncellenecek leaderboard dönemi: daily, weekly veya monthly.
        public string Period { get; set; } = string.Empty;

        // Dönemin anahtarı: 2026-11-09, 2026-W46 veya 2026-11.
        public string PeriodKey { get; set; } = string.Empty;

        // Ülke bazlı leaderboard için ülke kodu.
        // NULL ise global leaderboard kaydıdır.
        public string? CountryCode { get; set; }

        // Kullanıcının ilgili dönemdeki toplam puanı.
        public int TotalScore { get; set; }

        // Kullanıcının ilgili dönemde çözdüğü puzzle sayısı.
        public int SolvedCount { get; set; }

        // Outbox görevinin tamamlanıp tamamlanmadığı.
        public bool IsProcessed { get; set; } = false;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAtUtc { get; set; }
    }
}
