namespace DailyChessPuzzleApi.Models
{
    // Bir Redis hedefinin güncelleme durumunu belirtir.
    public enum LeaderboardCacheOutboxDeliveryStatus
    {
        // Henüz işlenmedi.
        Pending = 0,

        // Bir Worker tarafından işleniyor.
        Processing = 1,

        // Redis güncellemesi başarıyla tamamlandı.
        Completed = 2,

        // İşlem başarısız oldu.
        Failed = 3
    }
}
