
using DailyChessPuzzleApi.Services;
using DailyChessPuzzleApi.Services.Interfaces;
using DailyChessPuzzleApi.Data;
using DailyChessPuzzleApi.Middlewares;
using DailyChessPuzzleApi.Responses;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

using Serilog;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

// İşletim sistemine göre log dosyalarının tutulacağı klasör belirlenir.
var logDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? "C:/DailyChess/logs"
    : "/var/log/dailychess";

// Log klasörü mevcut değilse oluşturulur.
Directory.CreateDirectory(logDirectory);

// Log dosyasının yolu belirlenir.
var logFilePath = Path.Combine(logDirectory, "log-.txt");

// Uygulamanın log kayıtları Serilog üzerinden yönetilir.
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.File(
            path: logFilePath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 10,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"
        );
});

// PostgreSQL bağlantısı ApplicationDbContext üzerinden yönetilir.
// Bağlantı bilgileri appsettings veya User Secrets içerisindeki
// DefaultConnection ayarından okunur.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// API Controller servisleri kaydedilir.
builder.Services.AddControllers();

// [ApiController] tarafından otomatik gerçekleştirilen
// model doğrulama hatalarının BaseResponse formatında dönmesi sağlanır.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        // Model doğrulama hataları toplanır.
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors)
            .Select(x => x.ErrorMessage)
            .ToList();

        // Hata cevabı ResponseFactory üzerinden oluşturulur.
        var response = ResponseFactory.Failure<object>(
            context.HttpContext,
            "One or more validation errors occurred.",
            StatusCodes.Status400BadRequest,
            "VALIDATION_ERROR",
            errors
        );

        // HTTP 400 Bad Request ile cevap döndürülür.
        return new BadRequestObjectResult(response);
    };
});

// PuzzleService, IPuzzleService arayüzü üzerinden kullanılır.
builder.Services.AddScoped<IPuzzleService, PuzzleService>();

// LeaderboardService, ILeaderboardService arayüzü üzerinden kullanılır.
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();

builder.Services.AddScoped<IDailyPuzzleService, DailyPuzzleService>();

builder.Services.AddHostedService<DailyPuzzleBackgroundService>();

// API endpointlerinin Swagger üzerinden görüntülenmesini sağlar.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Her HTTP isteğine benzersiz bir Correlation ID atanır.
app.UseMiddleware<CorrelationIdMiddleware>();

// Uygulama içerisinde oluşan beklenmeyen hatalar yakalanır
// ve standart BaseResponse formatında döndürülür.
app.UseMiddleware<GlobalExceptionMiddleware>();

// HTTP isteklerinin Serilog üzerinden loglanmasını sağlar.
app.UseSerilogRequestLogging();

// Swagger yalnızca Development ortamında etkinleştirilir.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTP isteklerini HTTPS'e yönlendirir.
app.UseHttpsRedirection();

// Yetkilendirme middleware'i etkinleştirilir.
app.UseAuthorization();

// Controller endpointleri uygulamaya tanıtılır.
app.MapControllers();

app.Run();
