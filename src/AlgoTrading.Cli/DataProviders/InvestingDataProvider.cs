using ConsoleApp1.Models;

public class InvestingDataProvider : IDataProvider
{
    public async Task<List<StockPriceHistory>> GetHistoricalDataAsync(string ticker, DateTime start, DateTime end)
    {
        var results = new List<StockPriceHistory>();
        switch (ticker)
        {
            case "GLE.PA": // Société Générale
                results = await CsvHelperUtils.ReadCsvAsync("/home/mika/Downloads/Societe Generale Stock Price History.csv");
                break;
            case "CA.PA": // Carrefour
                results = await CsvHelperUtils.ReadCsvAsync("/home/mika/Downloads/Carrefour Stock Price History.csv");
                break;
            case "DSY.PA": // Dassault Systèmes
                results = await CsvHelperUtils.ReadCsvAsync("/home/mika/Downloads/Dassault Systemes Stock Price History.csv");
                break;
            case "STM.PA": // STMicroelectronics
            case "STM": // STMicroelectronics
                results = await CsvHelperUtils.ReadCsvAsync("/home/mika/Downloads/STMicroelectronics Stock Price History.csv");
                break;
            case "AI.PA": // Air Liquide
                results = await CsvHelperUtils.ReadCsvAsync("/home/mika/Downloads/Air Liquide Stock Price History.csv");
                break;
            case "ACC.PA": // Accor
            case "AC.PA": // Accor
                results = await CsvHelperUtils.ReadCsvAsync("/home/mika/Downloads/Accor Stock Price History.csv");
                break;
            default:
                throw new NotImplementedException($"Ticker {ticker} is not supported.");
        }

        return results
            .Where(r => r.Date >= start && r.Date <= end)
            .OrderBy(r => r.Date)
            .ToList();
    }
}