using ConsoleApp1.Models;

public interface IDataProvider
{
    Task<List<StockPriceHistory>> GetHistoricalDataAsync(string ticker, DateTime start, DateTime end);
}