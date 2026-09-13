using ConsoleApp1.Maps;
using ConsoleApp1.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

public static class CsvHelperUtils
{
    public static List<StockPriceHistory> ReadCsv(string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ","
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<StockPriceHistoryMap>();
        var records = csv.GetRecords<StockPriceHistory>().ToList();
        return records;
    }

    public async static Task<List<StockPriceHistory>> ReadCsvAsync(string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ","
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<StockPriceHistoryMap>();
        var records = new List<StockPriceHistory>();

        await foreach (var record in csv.GetRecordsAsync<StockPriceHistory>())
        {
            records.Add(record);
        }

        return records;
    }
}