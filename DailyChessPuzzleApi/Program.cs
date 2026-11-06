
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


// ==================================================
// 1. LOG DOSYASI YAPILANDIRMASI
// ==================================================

// İşletim sistemine göre log dosyalarının tutulacağı klasör belirlenir.
var logDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? "C:/DailyChess/logs"
    : "/var/log/dailychess";

// Log klasörü mevcut değilse oluşturulur.
Directory.CreateDirectory(logDirectory);

// Log dosyasının yolu belirlenir.
var logFilePath = Path.Combine(logDirectory, "log-.txt");


// ==================================================
// 2. SERILOG YAPILANDIRMASI
// ==================================================

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


// ==================================================
// 3. VERİTABANI YAPILANDIRMASI
// ==================================================

// PostgreSQL bağlantısı ApplicationDbContext üzerinden yönetilir.
// Bağlantı bilgileri appsettings veya User Secrets içerisindeki
// DefaultConnection ayarından okunur.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);


// ==================================================
// 4. CONTROLLER VE MODEL DOĞRULAMA YAPILANDIRMASI
// ==================================================

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


// ==================================================
// 5. SERVICE (İŞ MANTIĞI) KAYITLARI
// ==================================================

// PuzzleService, IPuzzleService arayüzü üzerinden kullanılır.
builder.Services.AddScoped<IPuzzleService, PuzzleService>();

// LeaderboardService, ILeaderboardService arayüzü üzerinden kullanılır.
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();


// ==================================================
// 6. SWAGGER YAPILANDIRMASI
// ==================================================

// API endpointlerinin Swagger üzerinden görüntülenmesini sağlar.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// ==================================================
// 7. UYGULAMANIN OLUŞTURULMASI
// ==================================================

var app = builder.Build();


// ==================================================
// 8. MIDDLEWARE (ARA YAZILIM) YAPILANDIRMASI
// ==================================================

// Her HTTP isteğine benzersiz bir Correlation ID atanır.
app.UseMiddleware<CorrelationIdMiddleware>();

// Uygulama içerisinde oluşan beklenmeyen hatalar yakalanır
// ve standart BaseResponse formatında döndürülür.
app.UseMiddleware<GlobalExceptionMiddleware>();

// HTTP isteklerinin Serilog üzerinden loglanmasını sağlar.
app.UseSerilogRequestLogging();


// ==================================================
// 9. SWAGGER ARAYÜZÜ
// ==================================================

// Swagger yalnızca Development ortamında etkinleştirilir.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// ==================================================
// 10. HTTP İSTEK YÖNLENDİRME VE YETKİLENDİRME
// ==================================================

// HTTP isteklerini HTTPS'e yönlendirir.
app.UseHttpsRedirection();

// Yetkilendirme middleware'i etkinleştirilir.
app.UseAuthorization();

// Controller endpointleri uygulamaya tanıtılır.
app.MapControllers();


// ==================================================
// 11. UYGULAMANIN BAŞLATILMASI
// ==================================================

app.Run();
