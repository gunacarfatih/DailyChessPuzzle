
namespace DailyChessPuzzleApi.Services.Interfaces
{
    public interface IDailyPuzzleService
    {
        // Belirtilen tarih için günlük bulmaca atar.
        Task AssignDailyPuzzleAsync(
            DateOnly puzzleDate,
            CancellationToken cancellationToken = default
        );
    }
}
