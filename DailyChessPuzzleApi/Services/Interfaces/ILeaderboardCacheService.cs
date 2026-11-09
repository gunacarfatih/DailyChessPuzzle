
namespace DailyChessPuzzleApi.Services.Interfaces
{
    public interface ILeaderboardCacheService
    {
        // Kullanıcının leaderboard puanını günceller.
        Task UpdateScoreAsync(
            string key,
            string clientId,
            int totalScore,
            int solvedCount);

        // Günlük leaderboard puanını günceller ve versiyonu artırır.
        Task UpdateDailyScoreAsync(
            string key,
            string versionKey,
            string clientId,
            int totalScore,
            int solvedCount);

        // Günlük cache'in hazır olup olmadığını kontrol eder.
        Task<bool> IsDailyCacheReadyAsync(string key);

        // Günlük leaderboard cache'ini yeniden oluşturur.
        Task RebuildDailyAsync(
            string key,
            List<(string ClientId, int TotalScore, int SolvedCount)> entries);

        // Kullanıcının puan ve çözdüğü bulmaca sayısını getirir.
        Task<(int TotalScore, int SolvedCount)?> GetUserScoreAsync(
            string key,
            string clientId);

        // Leaderboard'daki ilk N kullanıcıyı getirir.
        Task<List<(string ClientId, int TotalScore, int SolvedCount)>>
            GetTopAsync(
                string key,
                int count);

        // Kullanıcının sıralamasını getirir.
        Task<long?> GetRankAsync(
            string key,
            string clientId);

        // Redis anahtarının mevcut olup olmadığını kontrol eder.
        Task<bool> ExistsAsync(string key);

        // Cache versiyonunu getirir.
        Task<long> GetVersionAsync(string versionKey);

        // Cache versiyonunu bir artırır.
        Task<long> IncrementVersionAsync(string versionKey);

        // Günlük leaderboard kayıtlarını birleştirerek
        // özel tarih aralığı cache'ini oluşturur.
        Task RebuildRangeAsync(
            string rangeKey,
            IReadOnlyCollection<string> dailyKeys,
            TimeSpan cacheDuration);

        // Leaderboard cache'ini baştan oluşturur.
        Task RebuildAsync(
            string key,
            List<(string ClientId, int TotalScore, int SolvedCount)> entries);
    }
}
