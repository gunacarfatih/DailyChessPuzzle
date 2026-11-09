
using DailyChessPuzzleApi.Services.Interfaces;

namespace DailyChessPuzzleApi.Services
{
    public class DailyPuzzleBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DailyPuzzleBackgroundService> _logger;

        public DailyPuzzleBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<DailyPuzzleBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // Arka plan servisi başladığında çalışır.
        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Daily puzzle background service started."
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Bugünün tarihini UTC üzerinden hesapla.
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);

                    // Scoped servisleri kullanabilmek için scope oluştur.
                    using var scope = _scopeFactory.CreateScope();

                    var dailyPuzzleService = scope.ServiceProvider
                        .GetRequiredService<IDailyPuzzleService>();

                    // Bugünün bulmacasını ata.
                    await dailyPuzzleService.AssignDailyPuzzleAsync(
                        today,
                        stoppingToken
                    );
                }
                catch (OperationCanceledException) when (
                    stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "An error occurred while assigning the daily puzzle."
                    );
                }

                // Bir sonraki UTC gece yarısına kadar bekle.
                var now = DateTime.UtcNow;
                var nextMidnight = now.Date.AddDays(1);
                var delay = nextMidnight - now;

                _logger.LogInformation(
                    "Next daily puzzle assignment check: {NextRun}",
                    nextMidnight
                );

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (
                    stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "Daily puzzle background service stopped."
            );
        }
    }
}
