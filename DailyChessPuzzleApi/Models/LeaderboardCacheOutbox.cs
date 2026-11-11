
namespace DailyChessPuzzleApi.Models
{
    public class LeaderboardCacheOutbox
    {
        public int Id { get; set; }

        // Bu olayın hangi PuzzleAttempt sonucunda oluştuğu.
        public int PuzzleAttemptId { get; set; }

        // Puan değişikliğinin gerçekleştiği kullanıcı.
        public string ClientId { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Bu olaya bağlı bağımsız Redis teslimatları.
        public ICollection<LeaderboardCacheOutboxDelivery> Deliveries
        { get; set; } = new List<LeaderboardCacheOutboxDelivery>();
    }
}
