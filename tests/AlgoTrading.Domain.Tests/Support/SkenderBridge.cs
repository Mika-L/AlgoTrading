using AlgoTrading.Domain.MarketData;
using Skender.Stock.Indicators;

namespace AlgoTrading.Domain.Tests.Support;

/// <summary>
/// Pont vers <c>Skender.Stock.Indicators</c> (MIT, en <see cref="decimal"/>), utilisé
/// <b>uniquement</b> comme oracle différentiel dans les tests — jamais en production.
/// </summary>
public static class SkenderBridge
{
    public static IReadOnlyList<Quote> ToQuotes(BarSeries series) =>
    [
        .. series.Select(static bar => new Quote
        {
            Date = bar.Date.ToDateTime(TimeOnly.MinValue),
            Open = bar.Open,
            High = bar.High,
            Low = bar.Low,
            Close = bar.Close,
            Volume = bar.Volume,
        }),
    ];
}
