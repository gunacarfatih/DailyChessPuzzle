
using DailyChessPuzzleApi.DTOs;

namespace DailyChessPuzzleApi.Services.Interfaces
{
    public interface ILeaderboardService
    {
        Task<LeaderboardResponse> GetLeaderboardAsync(
            string period,
            string? country,
            string? clientId,
            CancellationToken cancellationToken = default
        );
    }
}
