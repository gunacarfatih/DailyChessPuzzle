namespace DailyChessPuzzleApi.Responses
{
    public static class ResponseFactory
    {
        // Başarılı API cevapları oluşturur.
        public static BaseResponse<T> Success<T>(
            HttpContext context,
            string message,
            T? data,
            int status = StatusCodes.Status200OK)
        {
            return Create(
                context,
                success: true,
                message: message,
                status: status,
                data: data
            );
        }

        // Hatalı API cevapları oluşturur.
        public static BaseResponse<T> Failure<T>(
            HttpContext context,
            string message,
            int status,
            string? errorCode = null,
            List<string>? errors = null)
        {
            return Create<T>(
                context,
                success: false,
                message: message,
                status: status,
                errors: errors,
                errorCode: errorCode
            );
        }

        // Bütün cevapların ortak oluşturulma işlemi.
        private static BaseResponse<T> Create<T>(
            HttpContext context,
            bool success,
            string message,
            int status,
            T? data = default,
            List<string>? errors = null,
            string? errorCode = null)
        {
            // Correlation ID varsa kullanılır.
            // Yoksa ASP.NET Core TraceIdentifier kullanılır.
            var traceId = context.Response.Headers["X-Correlation-ID"]
                .FirstOrDefault() ?? context.TraceIdentifier;

            return new BaseResponse<T>
            {
                Success = success,
                Message = message,
                Status = status,
                Data = data,
                Errors = errors,
                ErrorCode = errorCode,
                TraceId = traceId,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
