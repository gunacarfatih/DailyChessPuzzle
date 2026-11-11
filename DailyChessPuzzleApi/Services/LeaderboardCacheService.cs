
using System.Globalization;
using DailyChessPuzzleApi.Services.Interfaces;
using StackExchange.Redis;

namespace DailyChessPuzzleApi.Services
{
    public class LeaderboardCacheService : ILeaderboardCacheService
    {
        private readonly IDatabase _database;

        // Skor, üye Hash'i, revision Hash'i ve günlük versiyon
        // tek Lua Script çalıştırmasıyla güncellenir.
        private const string UpdateScoreScript = """
            local currentRevision = redis.call('HGET', KEYS[3], ARGV[1])
            local incomingRevision = tonumber(ARGV[4])

            if currentRevision and incomingRevision <= tonumber(currentRevision) then
                return 0
            end

            local oldMember = redis.call('HGET', KEYS[2], ARGV[1])

            if oldMember then
                redis.call('ZREM', KEYS[1], oldMember)
            end

            redis.call('ZADD', KEYS[1], ARGV[3], ARGV[2])
            redis.call('HSET', KEYS[2], ARGV[1], ARGV[2])
            redis.call('HSET', KEYS[3], ARGV[1], ARGV[4])

            if #KEYS >= 4 then
                redis.call('INCR', KEYS[4])
            end

            return 1
            """;

        // Cache değiştirilirken mevcut cache'teki daha yeni revision'ları
        // korur. Böylece rebuild sırasında gelen güncellemeler kaybolmaz.
        private const string ReplaceCacheScript = """
            local liveRevisions = redis.call('HGETALL', KEYS[3])

            for i = 1, #liveRevisions, 2 do
                local clientId = liveRevisions[i]
                local liveRevision = tonumber(liveRevisions[i + 1])

                local tempRevisionValue = redis.call('HGET', KEYS[6], clientId)
                local tempRevision = tempRevisionValue and tonumber(tempRevisionValue) or -1

                if liveRevision > tempRevision then
                    local liveMember = redis.call('HGET', KEYS[2], clientId)

                    if liveMember then
                        local liveScore = redis.call('ZSCORE', KEYS[1], liveMember)

                        if liveScore then
                            local oldTempMember = redis.call('HGET', KEYS[5], clientId)

                            if oldTempMember then
                                redis.call('ZREM', KEYS[4], oldTempMember)
                            end

                            redis.call('ZADD', KEYS[4], liveScore, liveMember)
                            redis.call('HSET', KEYS[5], clientId, liveMember)
                            redis.call('HSET', KEYS[6], clientId, liveRevision)
                        end
                    end
                end
            end

            redis.call('DEL', KEYS[1], KEYS[2], KEYS[3])

            if redis.call('EXISTS', KEYS[4]) == 1 then
                redis.call('RENAME', KEYS[4], KEYS[1])
            end

            if redis.call('EXISTS', KEYS[5]) == 1 then
                redis.call('RENAME', KEYS[5], KEYS[2])
            end

            if redis.call('EXISTS', KEYS[6]) == 1 then
                redis.call('RENAME', KEYS[6], KEYS[3])
            end

            local ttl = tonumber(ARGV[1])

            if ttl > 0 then
                redis.call('EXPIRE', KEYS[1], ttl)
                redis.call('EXPIRE', KEYS[2], ttl)
                redis.call('EXPIRE', KEYS[3], ttl)
            end

            if #KEYS >= 7 then
                redis.call('SET', KEYS[7], '1')
            end

            return 1
            """;

        public LeaderboardCacheService(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }

        public async Task UpdateScoreAsync(
            string key,
            string clientId,
            int totalScore,
            int solvedCount,
            long revision)
        {
            await ExecuteScoreUpdateAsync(
                key,
                null,
                clientId,
                totalScore,
                solvedCount,
                revision);
        }

        public async Task UpdateDailyScoreAsync(
            string key,
            string versionKey,
            string clientId,
            int totalScore,
            int solvedCount,
            long revision)
        {
            await ExecuteScoreUpdateAsync(
                key,
                versionKey,
                clientId,
                totalScore,
                solvedCount,
                revision);
        }

        private async Task ExecuteScoreUpdateAsync(
            string key,
            string? versionKey,
            string clientId,
            int totalScore,
            int solvedCount,
            long revision)
        {
            if (totalScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalScore));
            }

            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            string member = CreateMember(clientId, solvedCount);

            string membersKey = GetMemberKey(key);
            string revisionsKey = GetRevisionKey(key);

            RedisKey[] keys;

            if (versionKey == null)
            {
                keys =
                [
                    key,
                    membersKey,
                    revisionsKey
                ];
            }
            else
            {
                keys =
                [
                    key,
                    membersKey,
                    revisionsKey,
                    versionKey
                ];
            }

            RedisValue[] values =
            [
                clientId,
                member,
                -(double)totalScore,
                revision
            ];

            await _database.ScriptEvaluateAsync(
                UpdateScoreScript,
                keys,
                values);
        }

        public async Task<bool> IsDailyCacheReadyAsync(string key)
        {
            return await _database.KeyExistsAsync(
                GetDailyReadyKey(key));
        }

        public async Task RebuildDailyAsync(
            string key,
            List<(string ClientId, int TotalScore, int SolvedCount, long Revision)> entries)
        {
            await ReplaceCacheAsync(
                key,
                entries,
                readyKey: GetDailyReadyKey(key));
        }

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

            double numericScore = (double)score;
            int totalScore = checked((int)(-numericScore));

            return (
                totalScore,
                decoded.SolvedCount);
        }

        public async Task<List<(string ClientId, int TotalScore, int SolvedCount)>>
            GetTopAsync(string key, int count)
        {
            if (count <= 0)
            {
                return [];
            }

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

        public async Task<bool> ExistsAsync(string key)
        {
            return await _database.KeyExistsAsync(key);
        }

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

        public Task<long> IncrementVersionAsync(string versionKey)
        {
            return _database.StringIncrementAsync(versionKey);
        }

        // Birden fazla günlük leaderboard verisini birleştirir.
        // Her kullanıcı için en yüksek revision değeri korunur.
        public async Task RebuildRangeAsync(
            string rangeKey,
            IReadOnlyCollection<string> dailyKeys,
            TimeSpan cacheDuration)
        {
            var aggregated = new Dictionary<
                string,
                (int TotalScore, int SolvedCount, long Revision)>();

            foreach (string dailyKey in dailyKeys)
            {
                var dailyEntries =
                    await _database.SortedSetRangeByRankWithScoresAsync(
                        dailyKey,
                        0,
                        -1,
                        Order.Ascending);

                string dailyRevisionsKey = GetRevisionKey(dailyKey);

                foreach (var entry in dailyEntries)
                {
                    var decoded = DecodeMember(
                        entry.Element.ToString());

                    int totalScore = checked(
                        (int)(-entry.Score));

                    RedisValue revisionValue = await _database.HashGetAsync(
                        dailyRevisionsKey,
                        decoded.ClientId);

                    long revision = 0;

                    if (revisionValue.HasValue)
                    {
                        long.TryParse(
                            revisionValue.ToString(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out revision);
                    }

                    if (aggregated.TryGetValue(
                        decoded.ClientId,
                        out var existing))
                    {
                        aggregated[decoded.ClientId] = (
                            checked(existing.TotalScore + totalScore),
                            checked(existing.SolvedCount + decoded.SolvedCount),
                            Math.Max(existing.Revision, revision));
                    }
                    else
                    {
                        aggregated[decoded.ClientId] = (
                            totalScore,
                            decoded.SolvedCount,
                            revision);
                    }
                }
            }

            var entries = aggregated
                .Select(x => (
                    ClientId: x.Key,
                    TotalScore: x.Value.TotalScore,
                    SolvedCount: x.Value.SolvedCount,
                    Revision: x.Value.Revision))
                .ToList();

            await ReplaceCacheAsync(
                rangeKey,
                entries,
                cacheDuration);
        }

        public async Task RebuildAsync(
            string key,
            List<(string ClientId, int TotalScore, int SolvedCount, long Revision)> entries)
        {
            await ReplaceCacheAsync(key, entries);
        }

        private async Task ReplaceCacheAsync(
            string key,
            List<(string ClientId, int TotalScore, int SolvedCount, long Revision)> entries,
            TimeSpan? cacheDuration = null,
            string? readyKey = null)
        {
            string membersKey = GetMemberKey(key);
            string revisionsKey = GetRevisionKey(key);

            string suffix = Guid.NewGuid().ToString("N");

            string tempKey = $"{key}:rebuild:{suffix}";
            string tempMembersKey = $"{membersKey}:rebuild:{suffix}";
            string tempRevisionsKey = $"{revisionsKey}:rebuild:{suffix}";

            try
            {
                if (entries.Count > 0)
                {
                    var sortedSetEntries =
                        new SortedSetEntry[entries.Count];

                    var hashEntries =
                        new HashEntry[entries.Count];

                    var revisionEntries =
                        new HashEntry[entries.Count];

                    for (int i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];

                        if (entry.TotalScore < 0)
                        {
                            throw new ArgumentOutOfRangeException(
                                nameof(entries),
                                "TotalScore cannot be negative.");
                        }

                        if (entry.Revision < 0)
                        {
                            throw new ArgumentOutOfRangeException(
                                nameof(entries),
                                "Revision cannot be negative.");
                        }

                        string member = CreateMember(
                            entry.ClientId,
                            entry.SolvedCount);

                        sortedSetEntries[i] = new SortedSetEntry(
                            member,
                            -(double)entry.TotalScore);

                        hashEntries[i] = new HashEntry(
                            entry.ClientId,
                            member);

                        revisionEntries[i] = new HashEntry(
                            entry.ClientId,
                            entry.Revision);
                    }

                    await _database.SortedSetAddAsync(
                        tempKey,
                        sortedSetEntries);

                    await _database.HashSetAsync(
                        tempMembersKey,
                        hashEntries);

                    await _database.HashSetAsync(
                        tempRevisionsKey,
                        revisionEntries);
                }

                long ttlSeconds = 0;

                if (cacheDuration.HasValue)
                {
                    if (cacheDuration.Value <= TimeSpan.Zero)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(cacheDuration),
                            "Cache duration must be greater than zero.");
                    }

                    ttlSeconds = checked(
                        (long)Math.Ceiling(
                            cacheDuration.Value.TotalSeconds));
                }

                RedisKey[] keys =
                [
                    key,
                    membersKey,
                    revisionsKey,
                    tempKey,
                    tempMembersKey,
                    tempRevisionsKey
                ];

                if (readyKey != null)
                {
                    keys =
                    [
                        key,
                        membersKey,
                        revisionsKey,
                        tempKey,
                        tempMembersKey,
                        tempRevisionsKey,
                        readyKey
                    ];
                }

                RedisValue[] values =
                [
                    ttlSeconds
                ];

                await _database.ScriptEvaluateAsync(
                    ReplaceCacheScript,
                    keys,
                    values);
            }
            catch
            {
                await _database.KeyDeleteAsync(tempKey);
                await _database.KeyDeleteAsync(tempMembersKey);
                await _database.KeyDeleteAsync(tempRevisionsKey);

                throw;
            }
        }

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

        private static string GetMemberKey(string key)
        {
            return $"{key}:members";
        }

        private static string GetRevisionKey(string key)
        {
            return $"{key}:revisions";
        }

        private static string GetDailyReadyKey(string key)
        {
            return $"{key}:ready";
        }
    }
}
