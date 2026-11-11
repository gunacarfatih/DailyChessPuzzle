
namespace DailyChessPuzzleApi.Models
{
    public class LeaderboardScore
    {
        public int Id { get; set; }

        // Kullanıcının kimliği.
        public string ClientId { get; set; } = string.Empty;

        // Liderlik tablosunun dönemi: daily, weekly, monthly veya alltime.
        public string Period { get; set; } = string.Empty;

        // Dönemin anahtarı: 2026-11-09, 2026-W46 veya 2026-11.
        public string PeriodKey { get; set; } = string.Empty;

        // Ülke kodu. Global kayıtlar için null.
        public string? CountryCode { get; set; }

        // Kullanıcının ilgili dönemdeki toplam puanı.
        public int TotalScore { get; set; }

        // Kullanıcının ilgili dönemde çözdüğü bulmaca sayısı.
        public int SolvedCount { get; set; }

        // Bu skorun hangi Outbox güncellemesine ait olduğunu belirtir.
        // Daha büyük değer, daha yeni bir güncelleme anlamına gelir.
        public long Revision { get; set; }

        // Son güncelleme zamanı.
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
