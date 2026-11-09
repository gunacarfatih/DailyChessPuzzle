
namespace DailyChessPuzzleApi.DTOs
{
    public class LeaderboardPeriodDto
    {
        // İlgili sıralama tablosunun dönemi.
        public string Period { get; set; } = string.Empty;

        // Sıralamanın kapsadığı tarih aralığı.
        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        // Sıralamadaki kullanıcılar.
        public List<LeaderboardEntryDto> Rankings { get; set; } = new();

        // İsteği yapan kullanıcının kendi sıralama bilgisi.
        public LeaderboardEntryDto? CurrentUser { get; set; }
    }
}
