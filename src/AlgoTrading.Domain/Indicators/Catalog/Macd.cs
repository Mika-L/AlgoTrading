using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Moving Average Convergence Divergence : écart de deux MME, sa ligne de signal et
/// l'histogramme. Ex-<c>MACD</c>.
/// </summary>
public sealed class Macd : IndicatorBase
{
    public const string Kind = "Macd";

    public Macd(int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        FastPeriod = RequirePositive(fastPeriod, nameof(fastPeriod));
        SlowPeriod = RequirePositive(slowPeriod, nameof(slowPeriod));
        SignalPeriod = RequirePositive(signalPeriod, nameof(signalPeriod));

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException($"La période courte ({fastPeriod}) doit être inférieure à la période longue ({slowPeriod}).", nameof(fastPeriod));
        }

        Descriptor = IndicatorDescriptor.Of(Kind, ("fast", fastPeriod), ("slow", slowPeriod), ("signal", signalPeriod));
    }

    public int FastPeriod { get; }

    public int SlowPeriod { get; }

    public int SignalPeriod { get; }

    public override IndicatorDescriptor Descriptor { get; }

    public override int WarmupBars => SlowPeriod - 1 + SignalPeriod - 1;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var close = bars.Close;

        var fast = Series.Ema(close, FastPeriod, out var fastFirst);
        var slow = Series.Ema(close, SlowPeriod, out var slowFirst);
        var macd = Series.Subtract(fast, slow, out var macdFirst, fastFirst, slowFirst);

        // La ligne de signal consomme une source déjà amorcée : le chaînage l'exige.
        var signal = Series.Ema(macd, SignalPeriod, out var signalFirst, macdFirst);
        var histogram = Series.Subtract(macd, signal, out var histogramFirst, macdFirst, signalFirst);

        return IndicatorResult.Create(Descriptor, bars.Count,
        [
            (IndicatorLines.Value, macd, macdFirst),
            (IndicatorLines.Signal, signal, signalFirst),
            (IndicatorLines.Histogram, histogram, histogramFirst),
        ]);
    }
}
