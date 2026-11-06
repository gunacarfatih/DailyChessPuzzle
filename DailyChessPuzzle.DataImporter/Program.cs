
using DailyChessPuzzleApi.Data;
using DailyChessPuzzle.DataImporter.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "DefaultConnection bulunamadı!");
}

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var context = new ApplicationDbContext(options);

var importer = new LichessPuzzleImporter(context);

var filePath = @"C:\DailyChess\Data\lichess_db_puzzle.csv.zst";

await importer.ImportAsync(filePath, 10000);
