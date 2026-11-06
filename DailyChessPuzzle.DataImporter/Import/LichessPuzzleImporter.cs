using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using DailyChessPuzzleApi.Data;
using DailyChessPuzzleApi.Models;
using Microsoft.EntityFrameworkCore;
using ZstdSharp;

namespace DailyChessPuzzle.DataImporter.Import
{
    public class LichessPuzzleImporter
    {
        private readonly ApplicationDbContext _context;

        private const int BatchSize = 500;

        public LichessPuzzleImporter(ApplicationDbContext context)
        {
            _context = context;
        }


        public async Task ImportAsync(string filePath, int maxRecords = 10000)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Lichess dosyası bulunamadı.", filePath);

            int importedCount = 0;
            int skippedCount = 0;

            using var fileStream = File.OpenRead(filePath);
            using var decompressionStream = new DecompressionStream(fileStream);
            using var reader = new StreamReader(decompressionStream);

            using var csv = new TextFieldParser(reader);

            csv.TextFieldType = FieldType.Delimited;
            csv.SetDelimiters(",");
            csv.HasFieldsEnclosedInQuotes = true;

            // CSV başlıklarını oku.
            var headers = csv.ReadFields();

            if (headers == null)
                throw new InvalidOperationException("CSV başlıkları okunamadı.");

            int puzzleIdIndex = Array.FindIndex(headers, h => h == "PuzzleId");
            int fenIndex = Array.FindIndex(headers, h => h == "FEN");
            int movesIndex = Array.FindIndex(headers, h => h == "Moves");
            int ratingIndex = Array.FindIndex(headers, h => h == "Rating");
            int popularityIndex = Array.FindIndex(headers, h => h == "Popularity");
            int themesIndex = Array.FindIndex(headers, h => h == "Themes");

            if (new[] { puzzleIdIndex, fenIndex, movesIndex, ratingIndex,
                popularityIndex, themesIndex }.Any(i => i < 0))
            {
                throw new InvalidOperationException("CSV içerisinde beklenen sütunlar bulunamadı.");
            }

            var batch = new List<LichessPuzzleCsvRow>();

            while (!csv.EndOfData && importedCount + skippedCount < maxRecords)
            {
                var fields = csv.ReadFields();

                if (fields == null)
                    continue;

                var requiredMaxIndex = new[]
                {
            puzzleIdIndex, fenIndex, movesIndex,
            ratingIndex, popularityIndex, themesIndex
        }.Max();

                if (fields.Length <= requiredMaxIndex)
                    continue;

                if (!int.TryParse(fields[ratingIndex], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int rating) ||
                    !int.TryParse(fields[popularityIndex], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int popularity))
                {
                    continue;
                }

                batch.Add(new LichessPuzzleCsvRow
                {
                    PuzzleId = fields[puzzleIdIndex],
                    FEN = fields[fenIndex],
                    Moves = fields[movesIndex],
                    Rating = rating,
                    Popularity = popularity,
                    Themes = fields[themesIndex]
                });

                if (batch.Count >= BatchSize)
                {
                    var result = await SaveBatchAsync(batch);

                    importedCount += result.imported;
                    skippedCount += result.skipped;

                    batch.Clear();

                    Console.WriteLine(
                        $"İşlenen kayıt: {importedCount + skippedCount}");
                }
            }

            if (batch.Count > 0)
            {
                var result = await SaveBatchAsync(batch);

                importedCount += result.imported;
                skippedCount += result.skipped;
            }

            Console.WriteLine("Aktarım tamamlandı.");
            Console.WriteLine($"Eklenen: {importedCount}");
            Console.WriteLine($"Atlanan: {skippedCount}");
        }



        private async Task<(int imported, int skipped)> SaveBatchAsync(
            List<LichessPuzzleCsvRow> batch)
        {
            // Boş PuzzleId değerlerini çıkar ve aynı ID'leri tekilleştir.
            var uniqueBatch = batch
                .Where(x => !string.IsNullOrWhiteSpace(x.PuzzleId))
                .GroupBy(x => x.PuzzleId)
                .Select(g => g.First())
                .ToList();

            // Veritabanında zaten bulunan PuzzleId değerlerini bul.
            var puzzleIds = uniqueBatch
                .Select(x => x.PuzzleId)
                .ToList();

            var existingIds = await _context.Puzzles
                .AsNoTracking()
                .Where(p => puzzleIds.Contains(p.LichessPuzzleId))
                .Select(p => p.LichessPuzzleId)
                .ToListAsync();

            var existingIdSet = existingIds.ToHashSet();

            // Yalnızca yeni bulmacaları hazırla.
            var newPuzzles = uniqueBatch
                .Where(x => !existingIdSet.Contains(x.PuzzleId))
                .Select(x => new Puzzle
                {
                    LichessPuzzleId = x.PuzzleId,
                    Fen = x.FEN,
                    Moves = x.Moves,
                    Rating = x.Rating,
                    Popularity = x.Popularity,
                    Themes = x.Themes,
                    IsActive = true
                })
                .ToList();

            if (newPuzzles.Count > 0)
            {
                await _context.Puzzles.AddRangeAsync(newPuzzles);
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
            }

            return (newPuzzles.Count, batch.Count - newPuzzles.Count);
        }

    }
}