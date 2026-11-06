
using System.Runtime.ExceptionServices;
using System.Net;
using DailyChessPuzzleApi.Responses;

namespace DailyChessPuzzleApi.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        // HTTP isteği boyunca oluşabilecek hataları yakalar.
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (
                context.RequestAborted.IsCancellationRequested)
            {
                // İstemci isteği iptal ettiğinde hata cevabı üretmeyiz.
                _logger.LogInformation(
                    "HTTP request was cancelled by the client."
                );

                throw;
            }
            catch (KeyNotFoundException ex)
            {
                await HandleExceptionAsync(
                    context,
                    ex,
                    HttpStatusCode.NotFound
                );
            }
            catch (InvalidOperationException ex)
            {
                await HandleExceptionAsync(
                    context,
                    ex,
                    HttpStatusCode.Conflict
                );
            }
            catch (ArgumentException ex)
            {
                await HandleExceptionAsync(
                    context,
                    ex,
                    HttpStatusCode.BadRequest
                );
            }
            catch (Exception ex)
            {
                // Yukarıdaki kategorilere girmeyen hatalar 500 döndürür.
                await HandleExceptionAsync(
                    context,
                    ex,
                    HttpStatusCode.InternalServerError
                );
            }
        }

        // Yakalanan exception için standart API hata cevabı oluşturur.
        private async Task HandleExceptionAsync(
            HttpContext context,
            Exception exception,
            HttpStatusCode statusCode)
        {
            // HTTP cevabı gönderilmeye başlandıysa yeni cevap yazılamaz.
            if (context.Response.HasStarted)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
                return;
            }

            // Hata ayrıntıları sunucu loglarına kaydedilir.
            _logger.LogError(
                exception,
                "Request failed. Method: {Method}, Path: {Path}, StatusCode: {StatusCode}",
                context.Request.Method,
                context.Request.Path,
                (int)statusCode
            );

            var isInternalServerError =
                statusCode == HttpStatusCode.InternalServerError;

            // Beklenmeyen sunucu hatalarında iç detayları istemciye göstermeyiz.
            var message = isInternalServerError
                ? "An unexpected error occurred."
                : exception.Message;

            // HTTP durum koduna göre hata kodu belirlenir.
            var errorCode = statusCode switch
            {
                HttpStatusCode.BadRequest => "BAD_REQUEST",
                HttpStatusCode.NotFound => "RESOURCE_NOT_FOUND",
                HttpStatusCode.Conflict => "CONFLICT",
                HttpStatusCode.InternalServerError => "INTERNAL_SERVER_ERROR",
                _ => "UNKNOWN_ERROR"
            };

            // Hata detayları yalnızca beklenen hata türlerinde döndürülür.
            var errors = isInternalServerError
                ? null
                : new List<string> { exception.Message };

            // Standart hata cevabı ResponseFactory üzerinden oluşturulur.
            var response = ResponseFactory.Failure<object>(
                context,
                message,
                (int)statusCode,
                errorCode,
                errors
            );

            // HTTP cevabı hazırlanır.
            context.Response.Clear();
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            // JSON formatında istemciye gönderilir.
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
