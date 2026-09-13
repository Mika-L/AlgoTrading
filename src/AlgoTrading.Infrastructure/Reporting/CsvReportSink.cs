using System.Globalization;
using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Reporting;
using CsvHelper;

namespace AlgoTrading.Infrastructure.Reporting;

/// <summary>Exporte la courbe d'actif et le relevé de trades — deux fichiers, un répertoire.</summary>
public sealed class CsvReportSink : IReportSink
{
    public string Name => "csv";

    public async Task<string> WriteAsync(BacktestResult result, string outputDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var stem = Path.Combine(outputDirectory, $"{Slug(result.StrategyName)}-{result.Fingerprint[..8]}");

        await WriteEquityAsync($"{stem}-equity.csv", result, cancellationToken).ConfigureAwait(false);
        await WriteTradesAsync($"{stem}-trades.csv", result, cancellationToken).ConfigureAwait(false);

        return $"{stem}-equity.csv";
    }

    private static async Task WriteEquityAsync(string path, BacktestResult result, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(path);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteField("Date");
        csv.WriteField("Equity");
        csv.WriteField("Cash");
        csv.WriteField("Invested");
        await csv.NextRecordAsync().ConfigureAwait(false);

        foreach (var point in result.EquityCurve)
        {
            cancellationToken.ThrowIfCancellationRequested();

            csv.WriteField(point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            csv.WriteField(point.Equity);
            csv.WriteField(point.Cash);
            csv.WriteField(point.Invested);
            await csv.NextRecordAsync().ConfigureAwait(false);
        }
    }

    private static async Task WriteTradesAsync(string path, BacktestResult result, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(path);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        foreach (var header in new[] { "Symbol", "OpenedOn", "ClosedOn", "Quantity", "EntryPrice", "ExitPrice", "Costs", "NetPnL", "ExitReason" })
        {
            csv.WriteField(header);
        }

        await csv.NextRecordAsync().ConfigureAwait(false);

        foreach (var trade in result.Trades)
        {
            cancellationToken.ThrowIfCancellationRequested();

            csv.WriteField(trade.Symbol.Ticker);
            csv.WriteField(trade.OpenedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            csv.WriteField(trade.ClosedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            csv.WriteField(trade.Quantity);
            csv.WriteField(trade.EntryPrice);
            csv.WriteField(trade.ExitPrice);
            csv.WriteField(trade.Costs);
            csv.WriteField(trade.NetPnL);
            csv.WriteField(trade.ExitReason);
            await csv.NextRecordAsync().ConfigureAwait(false);
        }
    }

    private static string Slug(string name) =>
        string.Concat(name.Select(static c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')).Trim('-');
}
