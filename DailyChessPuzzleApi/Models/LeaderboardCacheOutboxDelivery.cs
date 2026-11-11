
namespace DailyChessPuzzleApi.Models
{
    public class LeaderboardCacheOutboxDelivery
    {
        public int Id { get; set; }

        // Bağlı olduğu ana Outbox kaydı.
        public int OutboxId { get; set; }

        public LeaderboardCacheOutbox Outbox { get; set; } = null!;

        // Güncellenecek liderlik tablosunun dönemi.
        // daily, weekly, monthly, alltime
        public string Period { get; set; } = string.Empty;

        // Güncellenecek dönemin anahtarı.
        public string PeriodKey { get; set; } = string.Empty;

        // NULL ise global, dolu ise ülke sıralaması.
        public string? CountryCode { get; set; }

        // Redis Sorted Set anahtarı.
        public string CacheKey { get; set; } = string.Empty;

        // Redis'e gönderilecek toplam puan.
        public int TotalScore { get; set; }

        // Kullanıcının ilgili dönemde çözdüğü bulmaca sayısı.
        public int SolvedCount { get; set; }

        // Teslimatın mevcut durumu.
        public LeaderboardCacheOutboxDeliveryStatus Status { get; set; }
            = LeaderboardCacheOutboxDeliveryStatus.Pending;

        // Kaç kez işlenmeye çalışıldığını tutar.
        public int RetryCount { get; set; }

        // Son hata mesajı.
        public string? LastError { get; set; }

        // Tekrar denemenin planlandığı zaman.
        public DateTime? NextRetryAtUtc { get; set; }

        // Worker'ın teslimatı ne zamana kadar sahiplendiği.
        public DateTime? LockedUntilUtc { get; set; }

        // Teslimatı işleyen Worker'ın kimliği.
        public string? LockedBy { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAtUtc { get; set; }
    }
}
