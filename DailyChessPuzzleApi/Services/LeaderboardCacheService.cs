
using System.Globalization;
using DailyChessPuzzleApi.Services.Interfaces;
using StackExchange.Redis;

namespace DailyChessPuzzleApi.Services
{
    public class LeaderboardCacheService : ILeaderboardCacheService
    {
        private readonly IDatabase _database;

        public LeaderboardCacheService(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }

        // Kullanıcının leaderboard puanını Redis'e kaydeder.
        public async Task UpdateScoreAsync(
            string key,
            string clientId,
            int totalScore,
            int solvedCount)
        {
            string member = CreateMember(clientId, solvedCount);
            string membersKey = GetMemberKey(key);

            RedisValue oldMember = await _database.HashGetAsync(
                membersKey,
                clientId);

            if (oldMember.HasValue)
            {
                await _database.SortedSetRemoveAsync(
                    key,
                    oldMember.ToString());
            }

            await _database.SortedSetAddAsync(
                key,
                member,
                -(double)totalScore);

            await _database.HashSetAsync(
                membersKey,
                clientId,
                member);
        }

        // Günlük puanı günceller ve cache versiyonunu artırır.
        public async Task UpdateDailyScoreAsync(
            string key,
            string versionKey,
            string clientId,
            int totalScore,
            int solvedCount)
        {
            await UpdateScoreAsync(
                key,
                clientId,
                totalScore,
                solvedCount);

            await IncrementVersionAsync(versionKey);
        }

        // Günlük cache'in hazır olup olmadığını kontrol eder.
        public async Task<bool> IsDailyCacheReadyAsync(string key)
        {
            return await _database.KeyExistsAsync(
                GetDailyReadyKey(key));
        }

        // Günlük leaderboard cache'ini baştan oluşturur.
        public async Task RebuildDailyAsync(
            string key,
            List<(string ClientId, int TotalScore, int SolvedCount)> entries)
        {
            await RebuildAsync(key, entries);

            await _database.StringSetAsync(
                GetDailyReadyKey(key),
                "1");
        }

        // Kullanıcının puan ve çözdüğü bulmaca sayısını getirir.
        public async Task<(int TotalScore, int SolvedCount)?> GetUserScoreAsync(
            string key,
            string clientId)
        {
            string membersKey = GetMemberKey(key);

            RedisValue member = await _database.HashGetAsync(
                membersKey,
                clientId);

            if (!member.HasValue)
            {
                return null;
            }

            RedisValue score = await _database.SortedSetScoreAsync(
                key,
                member.ToString());

            if (!score.HasValue)
            {
                return null;
            }

            var decoded = DecodeMember(member.ToString());

            // RedisValue doğrudan negatiflenemez.
            double numericScore = (double)score;
            int totalScore = checked((int)(-numericScore));

            return (
                totalScore,
                decoded.SolvedCount);
        }

        // Leaderboard'daki ilk N kullanıcıyı getirir.
        public async Task<List<(string ClientId, int TotalScore, int SolvedCount)>>
            GetTopAsync(string key, int count)
        {
            var entries = await _database.SortedSetRangeByRankWithScoresAsync(
                key,
                0,
                count - 1,
                Order.Ascending);

            var result =
                new List<(string ClientId, int TotalScore, int SolvedCount)>();

            foreach (var entry in entries)
            {
                var decoded = DecodeMember(entry.Element.ToString());

                int totalScore = checked(
                    (int)(-entry.Score));

                result.Add((
                    decoded.ClientId,
                    totalScore,
                    decoded.SolvedCount));
            }

            return result;
        }

        // Kullanıcının Redis sıralamasını getirir.
        public async Task<long?> GetRankAsync(
            string key,
            string clientId)
        {
            string membersKey = GetMemberKey(key);

            RedisValue member = await _database.HashGetAsync(
                membersKey,
                clientId);

            if (!member.HasValue)
            {
                return null;
            }

            long? rank = await _database.SortedSetRankAsync(
                key,
                member.ToString(),
                Order.Ascending);

            return rank.HasValue
                ? rank.Value + 1
                : null;
        }

        // Redis anahtarının mevcut olup olmadığını kontrol eder.
        public async Task<bool> ExistsAsync(string key)
        {
            return await _database.KeyExistsAsync(key);
        }

        // Cache versiyonunu Redis'ten okur.
        public async Task<long> GetVersionAsync(string versionKey)
        {
            RedisValue value = await _database.StringGetAsync(versionKey);

            if (!value.HasValue)
            {
                return 0;
            }

            return long.TryParse(
                value.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long version)
                    ? version
                    : 0;
        }

        // Cache versiyonunu bir artırır.
        public Task<long> IncrementVersionAsync(string versionKey)
        {
            return _database.StringIncrementAsync(versionKey);
        }

        // Birden fazla günlük leaderboard verisini birleştirir.
        public async Task RebuildRangeAsync(
            string rangeKey,
            IReadOnlyCollection<string> dailyKeys,
            TimeSpan cacheDuration)
        {
            var aggregated = new Dictionary<
                string,
                (int TotalScore, int SolvedCount)>();

            foreach (string dailyKey in dailyKeys)
            {
                var dailyEntries =
                    await _database.SortedSetRangeByRankWithScoresAsync(
                        dailyKey,
                        0,
                        -1,
                        Order.Ascending);

                foreach (var entry in dailyEntries)
                {
                    var decoded = DecodeMember(
                        entry.Element.ToString());

                    int totalScore = checked(
                        (int)(-entry.Score));

                    if (aggregated.TryGetValue(
                        decoded.ClientId,
                        out var existing))
                    {
                        aggregated[decoded.ClientId] = (
                            checked(existing.TotalScore + totalScore),
                            checked(existing.SolvedCount + decoded.SolvedCount));
                    }
                    else
                    {
                        aggregated[decoded.ClientId] = (
                            totalScore,
                            decoded.SolvedCount);
                    }
                }
            }

            var entries = aggregated
                .Select(x => (
                    ClientId: x.Key,
                    TotalScore: x.Value.TotalScore,
                    SolvedCount: x.Value.SolvedCount))
                .ToList();

            await RebuildAsync(rangeKey, entries);

            await _database.KeyExpireAsync(
                rangeKey,
                cacheDuration);

            await _database.KeyExpireAsync(
                GetMemberKey(rangeKey),
                cacheDuration);
        }

        // Verilen leaderboard verisini Redis'e baştan yazar.
        public async Task RebuildAsync(
            string key,
            List<(string ClientId, int TotalScore, int SolvedCount)> entries)
        {
            string membersKey = GetMemberKey(key);

            await _database.KeyDeleteAsync(key);
            await _database.KeyDeleteAsync(membersKey);

            if (entries.Count == 0)
            {
                return;
            }

            var sortedSetEntries = new SortedSetEntry[entries.Count];
            var hashEntries = new HashEntry[entries.Count];

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                string member = CreateMember(
                    entry.ClientId,
                    entry.SolvedCount);

                sortedSetEntries[i] = new SortedSetEntry(
                    member,
                    -(double)entry.TotalScore);

                hashEntries[i] = new HashEntry(
                    entry.ClientId,
                    member);
            }

            await _database.SortedSetAddAsync(
                key,
                sortedSetEntries);

            await _database.HashSetAsync(
                membersKey,
                hashEntries);
        }

        // Redis sıralamasında kullanılacak üye bilgisini oluşturur.
        private static string CreateMember(
            string clientId,
            int solvedCount)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException(
                    "ClientId cannot be empty.",
                    nameof(clientId));
            }

            if (solvedCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(solvedCount));
            }

            int invertedSolvedCount = int.MaxValue - solvedCount;

            return $"{invertedSolvedCount:D10}:{clientId}";
        }

        // Redis üye bilgisinden ClientId ve SolvedCount değerlerini ayırır.
        private static (
            string ClientId,
            int SolvedCount) DecodeMember(string member)
        {
            int separatorIndex = member.IndexOf(':');

            if (separatorIndex < 0)
            {
                throw new FormatException(
                    "Invalid leaderboard Redis member format.");
            }

            string invertedSolvedCountText =
                member[..separatorIndex];

            string clientId =
                member[(separatorIndex + 1)..];

            if (!int.TryParse(
                invertedSolvedCountText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int invertedSolvedCount))
            {
                throw new FormatException(
                    "Invalid solved count in leaderboard Redis member.");
            }

            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new FormatException(
                    "ClientId cannot be empty in leaderboard Redis member.");
            }

            int solvedCount = int.MaxValue - invertedSolvedCount;

            return (clientId, solvedCount);
        }

        // Kullanıcı eşleştirmelerinin tutulduğu Redis HASH anahtarı.
        private static string GetMemberKey(string key)
        {
            return $"{key}:members";
        }

        // Günlük cache hazır bilgisinin tutulduğu Redis anahtarı.
        private static string GetDailyReadyKey(string key)
        {
            return $"{key}:ready";
        }
    }
}
