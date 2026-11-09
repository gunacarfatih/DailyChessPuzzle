
namespace DailyChessPuzzleApi.Services.Interfaces
{
    public interface ILeaderboardCacheService
    {
        // Sabit dönemlerin ZSET ve HASH verilerini günceller.
        // TotalScore ve SolvedCount mutlak değerlerdir.
        Task UpdateScoreAsync(
            string key,
            string clientId,
            int totalScore,
            int solvedCount);

        // Günlük kullanıcı verisini günceller ve
        // özel tarih aralığı önbelleklerinin versiyonunu artırır.
        Task UpdateDailyScoreAsync(
            string key,
            string versionKey,
            string clientId,
            int totalScore,
            int solvedCount);

        // Belirli bir kullanıcının puan bilgilerini getirir.
        Task<(int TotalScore, int SolvedCount)?> GetUserScoreAsync(
            string key,
            string clientId);

        // İlk N kullanıcıyı sıralı şekilde getirir.
        Task<List<(string ClientId, int TotalScore, int SolvedCount)>> GetTopAsync(
            string key,
            int count);

        // Kullanıcının rekabetçi sırasını getirir.
        Task<long?> GetRankAsync(
            string key,
            string clientId);

        // Redis'te sıralama verisinin bulunup bulunmadığını kontrol eder.
        Task<bool> ExistsAsync(string key);

        // Günlük verilerin değişiklik versiyonunu getirir.
        Task<long> GetVersionAsync(string versionKey);

        // Günlük Redis tablolarını birleştirerek
        // özel tarih aralığı için yeni bir ZSET + HASH oluşturur.
        Task RebuildRangeAsync(
            string rangeKey,
            IReadOnlyCollection<string> dailyKeys,
            TimeSpan cacheDuration);

        // PostgreSQL'den hesaplanan sabit dönem sıralamasını
        // Redis'e yeniden yükler.
        Task RebuildAsync(
            string key,
            List<(string ClientId, int TotalScore, int SolvedCount)> entries);
    }
}
