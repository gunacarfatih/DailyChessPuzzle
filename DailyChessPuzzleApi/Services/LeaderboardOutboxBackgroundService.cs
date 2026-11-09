
using DailyChessPuzzleApi.Services.Interfaces;

namespace DailyChessPuzzleApi.Services
{
    public class LeaderboardOutboxBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LeaderboardOutboxBackgroundService> _logger;

        public LeaderboardOutboxBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<LeaderboardOutboxBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Leaderboard Outbox Background Service başlatıldı."
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Her işlem için yeni bir DI scope oluşturulur.
                    using var scope = _scopeFactory.CreateScope();

                    // Outbox servisi DI üzerinden alınır.
                    var outboxService = scope.ServiceProvider
                        .GetRequiredService<ILeaderboardOutboxService>();

                    // Bekleyen görevler işlenir.
                    await outboxService.ProcessPendingAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    // Uygulama kapatılırken döngü kontrollü sonlandırılır.
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Leaderboard Outbox Background Service işleminde hata oluştu."
                    );
                }

                try
                {
                    // Bir sonraki kontrol için 10 saniye beklenir.
                    await Task.Delay(
                        TimeSpan.FromSeconds(10),
                        stoppingToken
                    );
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "Leaderboard Outbox Background Service durduruldu."
            );
        }
    }
}
