
using DailyChessPuzzleApi.DTOs;

namespace DailyChessPuzzleApi.Services.Interfaces
{
    public interface ILeaderboardService
    {
        Task<LeaderboardResponse> GetLeaderboardAsync(
            string? country,
            string? clientId,
            int limit,
            DateOnly? startDate,
            DateOnly? endDate,
            CancellationToken cancellationToken = default
        );
    }
}
