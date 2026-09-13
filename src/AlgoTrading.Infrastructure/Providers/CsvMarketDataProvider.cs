using System.Globalization;
using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.MarketData;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlgoTrading.Infrastructure.Providers;

public sealed class CsvProviderOptions
{
    public const string Section = "Providers:Csv";

    /// <summary>Répertoire des exports, lu en configuration — fini les chemins absolus codés en dur.</summary>
    public string Directory { get; set; } = "data/csv";
}

/// <summary>
/// Cotations depuis des exports CSV au format usuel
/// <c>Date,Open,High,Low,Close,Adj Close,Volume</c>, un fichier par titre.
/// </summary>
public sealed class CsvMarketDataProvider(IOptions<CsvProviderOptions> options, ILogger<CsvMarketDataProvider> logger) : IMarketDataProvider
{
    public string Name => "csv";

    public Task<IReadOnlyList<RawBar>> FetchAsync(Symbol symbol, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var directory = options.Value.Directory;
        var path = Path.Combine(directory, $"{symbol.Ticker}.csv");

        if (!File.Exists(path))
        {
            logger.LogWarning("{Symbol} : aucun fichier {Path}.", symbol, path);
            return Task.FromResult<IReadOnlyList<RawBar>>([]);
        }

        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, configuration);

        var bars = new List<RawBar>();

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!csv.TryGetField<DateTime>("Date", out var date))
            {
                continue;
            }

            var day = DateOnly.FromDateTime(date);

            if (day < from || day > to)
            {
                continue;
            }

            if (!csv.TryGetField<decimal>("Open", out var open) ||
                !csv.TryGetField<decimal>("High", out var high) ||
                !csv.TryGetField<decimal>("Low", out var low) ||
                !csv.TryGetField<decimal>("Close", out var close))
            {
                continue;
            }

            if (!csv.TryGetField<decimal>("Adj Close", out var adjusted) || adjusted <= 0m)
            {
                adjusted = close;
            }

            csv.TryGetField<long>("Volume", out var volume);

            bars.Add(new RawBar(day, open, high, low, close, adjusted, Math.Max(0L, volume)));
        }

        return Task.FromResult<IReadOnlyList<RawBar>>(bars);
    }
}
