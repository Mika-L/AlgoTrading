using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ConsoleApp1.Models;

public class YahooFinanceDownloader : IDataProvider
{
    private readonly HttpClient _httpClient;

    public static List<string> Cac40Tickers =>
    [
        "AC.PA",   // Accor
        "ACA.PA",  // Crédit Agricole
        "AI.PA",   // Air Liquide
        "AIR.PA",  // Airbus
        "ALO.PA",  // Alstom
        "ATO.PA",  // Atos
        "BNP.PA",  // BNP Paribas
        "CAP.PA",  // Capgemini
        "CA.PA",   // Carrefour
        "CS.PA",   // AXA
        "DG.PA",   // Vinci
        "DSY.PA",  // Dassault Systèmes
        "EL.PA",   // EssilorLuxottica
        "EN.PA",   // Bouygues
        "ENGI.PA", // Engie
        "ERA.PA",  // Eramet (si présent)
        "FR.PA",   // Veolia
        "GLE.PA",  // Société Générale
        "HO.PA",   // Thales
        "KER.PA",  // Kering
        "LR.PA",   // Legrand
        "MC.PA",   // LVMH
        "ML.PA",   // Michelin
        "MT.PA",   // ArcelorMittal
        "OR.PA",   // L'Oréal
        "PUB.PA",  // Publicis
        "RI.PA",   // Pernod Ricard
        "RMS.PA",  // Hermès
        "RNO.PA",  // Renault
        "SAF.PA",  // Safran
        "SAN.PA",  // Sanofi
        "SGO.PA",  // Saint-Gobain
        "STLA", //"STLA.PA", // Stellantis
        "STM",// "STM.PA",  // STMicroelectronics
        "SU.PA",   // Schneider Electric
        "SW.PA",   // Sodexo
        "TTE.PA",  // TotalEnergies
        "URW.PA",//"URW.AS",  // Unibail-Rodamco-Westfield (cotée à Amsterdam)
        "VIE.PA",  // Veolia Environnement
        "VIV.PA",  // Vivendi
        "WLN.PA"   // Worldline
    ];

    public YahooFinanceDownloader()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestVersion = new Version(2, 0);
    }

    public async Task<List<StockPriceHistory>> GetHistoricalDataAsync(string ticker, DateTime start, DateTime end)
    {
        var startUnix = ((DateTimeOffset)start).ToUnixTimeSeconds();
        var endUnix = ((DateTimeOffset)end).ToUnixTimeSeconds();

        string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{ticker}" +
                     $"?period1={startUnix}&period2={endUnix}&interval=1d&events=history&includeAdjustedClose=true";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        // var csvContent = await response.Content.ReadAsStringAsync();
        // return ParseCsv(csvContent);

        var jsonContent = await response.Content.ReadAsStringAsync();
        return ParseYahooFinanceJson(jsonContent);
    }

    public static List<StockPriceHistory> ParseYahooFinanceJson(string json)
    {
        var priceBars = new List<StockPriceHistory>();

        var root = JsonNode.Parse(json)?["chart"]?["result"]?[0];
        if (root == null)
            throw new InvalidOperationException("Invalid Yahoo Finance JSON format.");

        var timestamps = root["timestamp"]?.AsArray();
        var quotes = root["indicators"]?["quote"]?[0];
        var adjcloses = root["indicators"]?["adjclose"]?[0]?["adjclose"]?.AsArray();

        if (timestamps == null || quotes == null || adjcloses == null)
            throw new InvalidOperationException("Missing expected data in JSON.");

        var opens = quotes["open"]?.AsArray();
        var highs = quotes["high"]?.AsArray();
        var lows = quotes["low"]?.AsArray();
        var closes = quotes["close"]?.AsArray();
        var volumes = quotes["volume"]?.AsArray();

        for (int i = 0; i < timestamps.Count; i++)
        {
            var timestamp = timestamps[i]?.GetValue<long>() ?? 0;
            var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;

            priceBars.Add(new StockPriceHistory
            {
                Date = date.Date,
                Open = opens != null ? Convert.ToDecimal(opens[i]?.GetValue<double>() ?? 0) : 0,
                High = highs != null ? Convert.ToDecimal(highs[i]?.GetValue<double>() ?? 0) : 0,
                Low = lows != null ? Convert.ToDecimal(lows[i]?.GetValue<double>() ?? 0) : 0,
                Close = closes != null ? Convert.ToDecimal(closes[i]?.GetValue<double>() ?? 0) : 0,
                AdjustedClose = adjcloses != null ? Convert.ToDecimal(adjcloses[i]?.GetValue<double>() ?? 0) : 0,
                Volume = volumes != null ? Convert.ToInt64(volumes[i]?.GetValue<long>() ?? 0) : 0
            });
        }

        return priceBars;
    }

    private List<StockPriceHistory> ParseCsv(string csv)
    {
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var data = new List<StockPriceHistory>();

        for (int i = 1; i < lines.Length; i++) // skip header
        {
            var parts = lines[i].Split(',');

            if (parts.Length < 7 || parts[1] == "null") continue;

            try
            {
                var bar = new PriceBar
                {
                    Date = DateTime.Parse(parts[0]),
                    Open = decimal.Parse(parts[1], CultureInfo.InvariantCulture),
                    High = decimal.Parse(parts[2], CultureInfo.InvariantCulture),
                    Low = decimal.Parse(parts[3], CultureInfo.InvariantCulture),
                    Close = decimal.Parse(parts[4], CultureInfo.InvariantCulture),
                    AdjustedClose = decimal.Parse(parts[5], CultureInfo.InvariantCulture),
                    Volume = long.Parse(parts[6], CultureInfo.InvariantCulture)
                };

                var priceHistory = new StockPriceHistory
                {
                    StockId = 0, // Placeholder, set appropriately later
                    Date = bar.Date,
                    Close = bar.Close,
                    AdjustedClose = bar.AdjustedClose,
                    Open = bar.Open,
                    High = bar.High,
                    Low = bar.Low,
                    Volume = bar.Volume,
                };

                data.Add(priceHistory);
            }
            catch
            {
                // Ignore parsing errors (e.g., bad lines)
            }
        }

        return data;
    }
}
