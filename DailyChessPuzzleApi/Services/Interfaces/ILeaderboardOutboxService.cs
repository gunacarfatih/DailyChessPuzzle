
namespace DailyChessPuzzleApi.Services.Interfaces
{
    public interface ILeaderboardOutboxService
    {
        // PostgreSQL'deki bekleyen görevleri Redis'e gönderir.
        Task ProcessPendingAsync(CancellationToken cancellationToken);
    }
}
