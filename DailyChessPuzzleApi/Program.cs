
using ReusableCaching.Extensions;
using DailyChessPuzzleApi.Services;
using DailyChessPuzzleApi.Services.Interfaces;
using DailyChessPuzzleApi.Data;
using DailyChessPuzzleApi.Middlewares;
using DailyChessPuzzleApi.Responses;
using DailyChessPuzzleApi.Settings;

using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

using Serilog;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

// Leaderboard ayarlarını appsettings.json üzerinden yükle.
builder.Services.Configure<LeaderboardSettings>(
    builder.Configuration.GetSection("LeaderboardSettings"));

// Redis veya Memory cache altyapısını yapılandırır.
builder.Services.AddReusableCaching(builder.Configuration);

// Redis bağlantısını uygulama boyunca tek bir instance olarak kullanır.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisConnection = builder.Configuration
        .GetConnectionString("Redis");

    if (string.IsNullOrWhiteSpace(redisConnection))
    {
        throw new InvalidOperationException(
            "Redis connection string bulunamadı.");
    }

    // Redis bağlantı ayarlarını oluştur.
    var redisOptions = ConfigurationOptions.Parse(redisConnection);

    // Redis kapalı olsa bile uygulamanın açılmasına izin ver.
    // Bağlantı kesilirse ConnectionMultiplexer yeniden bağlanmayı dener.
    redisOptions.AbortOnConnectFail = false;

    return ConnectionMultiplexer.Connect(redisOptions);
});

// Leaderboard cache servisini DI sistemine kaydeder.
builder.Services.AddSingleton<
    ILeaderboardCacheService,
    LeaderboardCacheService>();

// İşletim sistemine göre log dosyalarının tutulacağı klasörü belirle.
var logDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? "C:/DailyChess/logs"
    : "/var/log/dailychess";

// Log klasörü mevcut değilse oluştur.
Directory.CreateDirectory(logDirectory);

// Log dosyasının yolunu belirle.
var logFilePath = Path.Combine(logDirectory, "log-.txt");

// Serilog yapılandırması.
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.File(
            path: logFilePath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 10,
            outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"
        );
});

// PostgreSQL bağlantısını ApplicationDbContext üzerinden yapılandır.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// API Controller servislerini kaydet.
builder.Services.AddControllers();

// Model doğrulama hatalarını BaseResponse formatında döndür.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors)
            .Select(x => x.ErrorMessage)
            .ToList();

        var response = ResponseFactory.Failure<object>(
            context.HttpContext,
            "One or more validation errors occurred.",
            StatusCodes.Status400BadRequest,
            "VALIDATION_ERROR",
            errors
        );

        return new BadRequestObjectResult(response);
    };
});

// PuzzleService kaydı.
builder.Services.AddScoped<IPuzzleService, PuzzleService>();

// LeaderboardService kaydı.
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();

// Leaderboard Outbox servisi.
builder.Services.AddScoped<
    ILeaderboardOutboxService,
    LeaderboardOutboxService>();

// Günlük puzzle atama servisi.
builder.Services.AddScoped<IDailyPuzzleService, DailyPuzzleService>();

// Günlük puzzle atama arka plan servisi.
builder.Services.AddHostedService<DailyPuzzleBackgroundService>();

// Leaderboard Outbox arka plan servisi.
builder.Services.AddHostedService<LeaderboardOutboxBackgroundService>();

// Swagger servisleri.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Her HTTP isteğine benzersiz Correlation ID atanır.
app.UseMiddleware<CorrelationIdMiddleware>();

// Beklenmeyen hataları yakalar.
app.UseMiddleware<GlobalExceptionMiddleware>();

// HTTP isteklerini loglar.
app.UseSerilogRequestLogging();

// Swagger yalnızca Development ortamında etkinleştirilir.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTPS yönlendirmesi.
app.UseHttpsRedirection();

// Yetkilendirme middleware'i.
app.UseAuthorization();

// Controller endpointlerini eşleştir.
app.MapControllers();

app.Run();
