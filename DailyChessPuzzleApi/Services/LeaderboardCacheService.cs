
using DailyChessPuzzleApi.Services.Interfaces;
using StackExchange.Redis;
using System.Globalization;

namespace DailyChessPuzzleApi.Services
{
    public class LeaderboardCacheService : ILeaderboardCacheService
    {
        private readonly IDatabase _database;

        // ZSET üyesinin başında çözülen bulmaca sayısının
        // ters çevrilmiş değeri tutulur.
        private const int MaxSolvedCount = int.MaxValue;

        public LeaderboardCacheService(IConnectionMultiplexer redis)
        {
            _database = redis.GetDatabase();
        }

        // Sabit dönemlerin puanını ve çözüm sayısını günceller.
        public async Task UpdateScoreAsync(
            string key,
            string clientId,
            int totalScore,
            int solvedCount)
        {
            if (totalScore < 0 || solvedCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalScore),
                    "Score and solved count cannot be negative.");
            }

            // Kullanıcının önceki ZSET üyesini bul.
            var memberKey = GetMemberKey(key);

            var oldMember = await _database.HashGetAsync(
                memberKey,
                clientId);

            // Eski üye varsa ZSET'ten kaldır.
            // Çünkü çözüm sayısı değiştiğinde üye anahtarı da değişir.
            if (!oldMember.IsNullOrEmpty)
            {
                await _database.SortedSetRemoveAsync(
                    key,
                    oldMember);
            }

            // Yüksek puanların önce gelmesi için negatif puan kullan.
            double redisScore = -totalScore;

            // Eşit puanda çözüm sayısı yüksek olan önce gelsin.
            // Eşit puan ve çözüm sayısında ClientId alfabetik sıralanır.
            string member = CreateMember(clientId, solvedCount);

            await _database.SortedSetAddAsync(
                key,
                member,
                redisScore);

            // ClientId -> ZSET üyesi eşlemesini sakla.
            await _database.HashSetAsync(
                memberKey,
                clientId,
                member);
        }

        // Günlük tabloyu günceller ve özel tarih aralığı
        // önbelleklerinin versiyonunu artırır.
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

            // Günlük veri değiştiğinde versiyonu artır.
            await _database.StringIncrementAsync(versionKey);
        }

        // Redis ZSET üzerinden ilk N kullanıcıyı getirir.
        public async Task<List<(string ClientId, int TotalScore, int SolvedCount)>> GetTopAsync(
            string key,
            int count)
        {
            if (count <= 0)
            {
                return new List<(string ClientId, int TotalScore, int SolvedCount)>();
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

                result.Add((
                    ClientId: decoded.ClientId,
                    TotalScore: checked((int)-entry.Score),
                    SolvedCount: decoded.SolvedCount));
            }

            return result;
        }

        // Belirli bir kullanıcının puan bilgilerini getirir.
        public async Task<(int TotalScore, int SolvedCount)?> GetUserScoreAsync(
            string key,
            string clientId)
        {
            var member = await _database.HashGetAsync(
                GetMemberKey(key),
                clientId);

            if (member.IsNullOrEmpty)
            {
                return null;
            }

            var redisScore = await _database.SortedSetScoreAsync(
                key,
                member);

            if (redisScore == null)
            {
                return null;
            }

            var decoded = DecodeMember(member.ToString());

            return (
                TotalScore: checked((int)-redisScore.Value),
                SolvedCount: decoded.SolvedCount);
        }

        // Kullanıcının rekabetçi sıralamasını getirir.
        public async Task<long?> GetRankAsync(
            string key,
            string clientId)
        {
            var member = await _database.HashGetAsync(
                GetMemberKey(key),
                clientId);

            if (member.IsNullOrEmpty)
            {
                return null;
            }

            var rank = await _database.SortedSetRankAsync(
                key,
                member,
                Order.Ascending);

            // Redis sıralaması sıfırdan başlar.
            return rank.HasValue ? rank.Value + 1 : null;
        }

        // Redis'te sıralama verisinin bulunup bulunmadığını kontrol eder.
        public async Task<bool> ExistsAsync(string key)
        {
            return await _database.KeyExistsAsync(key);
        }

        // Günlük verilerin değişiklik versiyonunu getirir.
        public async Task<long> GetVersionAsync(string versionKey)
        {
            var value = await _database.StringGetAsync(versionKey);

            if (value.IsNullOrEmpty)
            {
                return 0;
            }

            return long.TryParse(
                value.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var version)
                ? version
                : 0;
        }

        // Günlük Redis tablolarını birleştirerek özel tarih aralığı
        // için yeni bir ZSET ve HASH oluşturur.
        public async Task RebuildRangeAsync(
            string rangeKey,
            IReadOnlyCollection<string> dailyKeys,
            TimeSpan cacheDuration)
        {
            // Kullanıcı bazında toplam puan ve çözüm sayısı.
            var aggregates = new Dictionary<
                string,
                (long TotalScore, long SolvedCount)>();

            foreach (var dailyKey in dailyKeys)
            {
                // Günlük ZSET kayıtlarını Redis'ten getir.
                var entries = await _database.SortedSetRangeByRankWithScoresAsync(
                    dailyKey,
                    0,
                    -1,
                    Order.Ascending);

                foreach (var entry in entries)
                {
                    var decoded = DecodeMember(entry.Element.ToString());

                    long dailyScore = checked((long)-entry.Score);

                    if (aggregates.TryGetValue(
                        decoded.ClientId,
                        out var current))
                    {
                        aggregates[decoded.ClientId] = (
                            current.TotalScore + dailyScore,
                            current.SolvedCount + decoded.SolvedCount);
                    }
                    else
                    {
                        aggregates[decoded.ClientId] = (
                            dailyScore,
                            decoded.SolvedCount);
                    }
                }
            }

            // Toplanan verileri standart Redis kayıtlarına dönüştür.
            var rangeEntries = aggregates
                .Select(x => (
                    ClientId: x.Key,
                    TotalScore: checked((int)x.Value.TotalScore),
                    SolvedCount: checked((int)x.Value.SolvedCount)))
                .ToList();

            // Özel tarih aralığı ZSET ve HASH verilerini oluştur.
            await RebuildAsync(rangeKey, rangeEntries);

            // Özel tarih aralığı önbelleğine süre sınırı koy.
            await _database.KeyExpireAsync(
                rangeKey,
                cacheDuration);

            await _database.KeyExpireAsync(
                GetMemberKey(rangeKey),
                cacheDuration);
        }

        // PostgreSQL'den veya günlük Redis tablolarından
        // hesaplanan sıralamayı Redis'e yeniden yükler.
        public async Task RebuildAsync(
            string key,
            List<(string ClientId, int TotalScore, int SolvedCount)> entries)
        {
            var memberKey = GetMemberKey(key);

            // Eski ZSET ve HASH kayıtlarını temizle.
            await _database.KeyDeleteAsync(key);
            await _database.KeyDeleteAsync(memberKey);

            if (entries.Count == 0)
            {
                return;
            }

            var sortedSetEntries = entries
                .Select(entry =>
                {
                    if (entry.TotalScore < 0 ||
                        entry.SolvedCount < 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(entries),
                            "Score and solved count cannot be negative.");
                    }

                    return new SortedSetEntry(
                        CreateMember(entry.ClientId, entry.SolvedCount),
                        -entry.TotalScore);
                })
                .ToArray();

            var hashEntries = entries
                .Select(entry => new HashEntry(
                    entry.ClientId,
                    CreateMember(entry.ClientId, entry.SolvedCount)))
                .ToArray();

            // ZSET sıralama kayıtlarını yükle.
            await _database.SortedSetAddAsync(
                key,
                sortedSetEntries);

            // ClientId -> ZSET üyesi eşlemesini yükle.
            await _database.HashSetAsync(
                memberKey,
                hashEntries);
        }

        // Çözüm sayısını ve ClientId değerini bir ZSET üyesine dönüştürür.
        private static string CreateMember(
            string clientId,
            int solvedCount)
        {
            if (solvedCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(solvedCount));
            }

            int invertedSolvedCount = MaxSolvedCount - solvedCount;

            return $"{invertedSolvedCount:D10}:{clientId}";
        }

        // ZSET üyesinden ClientId ve çözüm sayısını çıkarır.
        private static (string ClientId, int SolvedCount) DecodeMember(
            string member)
        {
            int separatorIndex = member.IndexOf(':');

            if (separatorIndex < 0)
            {
                throw new FormatException(
                    "Invalid leaderboard Redis member.");
            }

            string invertedValue = member[..separatorIndex];
            string clientId = member[(separatorIndex + 1)..];

            int invertedSolvedCount = int.Parse(
                invertedValue,
                CultureInfo.InvariantCulture);

            int solvedCount = MaxSolvedCount - invertedSolvedCount;

            return (clientId, solvedCount);
        }

        // Her ZSET için ClientId -> ZSET üyesi HASH anahtarını oluşturur.
        private static string GetMemberKey(string key)
        {
            return $"{key}:members";
        }
    }
}
