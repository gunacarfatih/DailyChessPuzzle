
namespace DailyChessPuzzleApi.Services.Interfaces
{
    public interface ILeaderboardCacheService
    {
        // Kullanıcının leaderboard puanını revision kontrolüyle günceller.
        Task UpdateScoreAsync(
            string key,
            string clientId,
            int totalScore,
            int solvedCount,
            long revision);

        // Günlük leaderboard puanını ve cache versiyonunu
        // revision kontrolüyle atomik olarak günceller.
        Task UpdateDailyScoreAsync(
            string key,
            string versionKey,
            string clientId,
            int totalScore,
            int solvedCount,
            long revision);

        Task<bool> IsDailyCacheReadyAsync(string key);

        Task RebuildDailyAsync(
            string key,
            List<(string ClientId, int TotalScore, int SolvedCount, long Revision)> entries);

        Task<(int TotalScore, int SolvedCount)?> GetUserScoreAsync(
            string key,
            string clientId);

        Task<List<(string ClientId, int TotalScore, int SolvedCount)>>
            GetTopAsync(
                string key,
                int count);

        Task<long?> GetRankAsync(
            string key,
            string clientId);

        Task<bool> ExistsAsync(string key);

        Task<long> GetVersionAsync(string versionKey);

        Task<long> IncrementVersionAsync(string versionKey);

        Task RebuildRangeAsync(
            string rangeKey,
            IReadOnlyCollection<string> dailyKeys,
            TimeSpan cacheDuration);

        Task RebuildAsync(
            string key,
            List<(string ClientId, int TotalScore, int SolvedCount, long Revision)> entries);
    }
}
