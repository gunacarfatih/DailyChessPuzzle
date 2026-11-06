using DailyChessPuzzleApi.Services;
using DailyChessPuzzleApi.Services.Interfaces;
using DailyChessPuzzleApi.Data;
using Microsoft.EntityFrameworkCore;
using DailyChessPuzzleApi.Middlewares;
using Serilog;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

var logDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? "C:/DailyChess/logs"
    : "/var/log/dailychess";

Directory.CreateDirectory(logDirectory);

var logFilePath = Path.Combine(logDirectory, "log-.txt");

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

// PostgreSQL ve Entity Framework Core bağlantısı
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// Controller servisleri
builder.Services.AddControllers();

builder.Services.AddScoped<IPuzzleService, PuzzleService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();

// Swagger servisleri
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSerilogRequestLogging();

// Swagger yapılandırması
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
