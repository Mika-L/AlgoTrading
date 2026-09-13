using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace AlgoTrading.Infrastructure.Providers;

/// <summary>
/// Cotations quotidiennes depuis l'API graphique de Yahoo Finance.
/// <para>Le client HTTP est injecté et typé : plus de <c>HttpClient</c> construit à la volée
/// dans une boucle.</para>
/// <para>L'endpoint n'est pas contractuel et peut exiger un jeton sans préavis. En cas
/// d'échec durable, la source CSV reste disponible.</para>
/// </summary>
public sealed class YahooFinanceProvider(HttpClient client, ILogger<YahooFinanceProvider> logger) : IMarketDataProvider
{
    public string Name => "yahoo";

    public async Task<IReadOnlyList<RawBar>> FetchAsync(Symbol symbol, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var start = ToUnixSeconds(from);
        var end = ToUnixSeconds(to.AddDays(1));

        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"v8/finance/chart/{Uri.EscapeDataString(symbol.Ticker)}?period1={start}&period2={end}&interval=1d&events=div%2Csplit");

        var payload = await client.GetFromJsonAsync(url, YahooJson.Default.YahooChartResponse, cancellationToken).ConfigureAwait(false);

        var result = payload?.Chart?.Result?.FirstOrDefault();

        if (result?.Timestamp is not { Count: > 0 } timestamps)
        {
            logger.LogWarning("{Symbol} : la réponse ne contient aucune cotation.", symbol);
            return [];
        }

        var quote = result.Indicators?.Quote?.FirstOrDefault();
        var adjusted = result.Indicators?.AdjClose?.FirstOrDefault()?.AdjClose;

        if (quote is null)
        {
            return [];
        }

        var bars = new List<RawBar>(timestamps.Count);

        for (var i = 0; i < timestamps.Count; i++)
        {
            var open = At(quote.Open, i);
            var high = At(quote.High, i);
            var low = At(quote.Low, i);
            var close = At(quote.Close, i);

            // Yahoo laisse des trous : une séance incomplète est ignorée, pas devinée.
            if (open is null || high is null || low is null || close is null)
            {
                continue;
            }

            var adjustedClose = At(adjusted, i) ?? close.Value;
            var volume = quote.Volume is { } volumes && i < volumes.Count ? volumes[i] ?? 0L : 0L;
            var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).UtcDateTime);

            bars.Add(new RawBar(
                date,
                (decimal)open.Value,
                (decimal)high.Value,
                (decimal)low.Value,
                (decimal)close.Value,
                (decimal)adjustedClose,
                volume));
        }

        return bars;
    }

    private static double? At(IReadOnlyList<double?>? values, int index) =>
        values is not null && index < values.Count ? values[index] : null;

    private static long ToUnixSeconds(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
}

internal sealed record YahooChartResponse([property: JsonPropertyName("chart")] YahooChart? Chart);

internal sealed record YahooChart([property: JsonPropertyName("result")] IReadOnlyList<YahooResult>? Result);

internal sealed record YahooResult(
    [property: JsonPropertyName("timestamp")] IReadOnlyList<long>? Timestamp,
    [property: JsonPropertyName("indicators")] YahooIndicators? Indicators);

internal sealed record YahooIndicators(
    [property: JsonPropertyName("quote")] IReadOnlyList<YahooQuote>? Quote,
    [property: JsonPropertyName("adjclose")] IReadOnlyList<YahooAdjClose>? AdjClose);

internal sealed record YahooQuote(
    [property: JsonPropertyName("open")] IReadOnlyList<double?>? Open,
    [property: JsonPropertyName("high")] IReadOnlyList<double?>? High,
    [property: JsonPropertyName("low")] IReadOnlyList<double?>? Low,
    [property: JsonPropertyName("close")] IReadOnlyList<double?>? Close,
    [property: JsonPropertyName("volume")] IReadOnlyList<long?>? Volume);

internal sealed record YahooAdjClose([property: JsonPropertyName("adjclose")] IReadOnlyList<double?>? AdjClose);

[JsonSerializable(typeof(YahooChartResponse))]
internal sealed partial class YahooJson : JsonSerializerContext;
