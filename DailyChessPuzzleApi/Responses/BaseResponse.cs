
namespace DailyChessPuzzleApi.Responses
{
    public class BaseResponse<T>
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public int Status { get; set; }

        public T? Data { get; set; }

        public List<string>? Errors { get; set; }

        public string? ErrorCode { get; set; }

        public string? TraceId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
