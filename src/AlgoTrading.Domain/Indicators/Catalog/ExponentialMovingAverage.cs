using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Deux moyennes mobiles exponentielles, destinées à être croisées. Ex-<c>MME</c>.
/// <para>Le décalage « hier » qui cherchait <c>date.AddDays(-1)</c> — donc jamais rien un
/// lundi — est remplacé par un décalage d'une barre porté par la règle de croisement.</para>
/// </summary>
public sealed class ExponentialMovingAverage : IndicatorBase
{
    public const string Kind = "Ema";

    public ExponentialMovingAverage(int fastPeriod = 9, int slowPeriod = 20)
    {
        FastPeriod = RequirePositive(fastPeriod, nameof(fastPeriod));
        SlowPeriod = RequirePositive(slowPeriod, nameof(slowPeriod));

        if (fastPeriod >= slowPeriod)
        {
            throw new ArgumentException($"La période courte ({fastPeriod}) doit être inférieure à la période longue ({slowPeriod}).", nameof(fastPeriod));
        }

        Descriptor = IndicatorDescriptor.Of(Kind, ("fast", fastPeriod), ("slow", slowPeriod));
    }

    public int FastPeriod { get; }

    public int SlowPeriod { get; }

    public override IndicatorDescriptor Descriptor { get; }

    public override int WarmupBars => SlowPeriod - 1;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var close = bars.Close;

        var fast = Series.Ema(close, FastPeriod, out var fastFirst);
        var slow = Series.Ema(close, SlowPeriod, out var slowFirst);

        return IndicatorResult.Create(Descriptor, bars.Count,
        [
            (IndicatorLines.Fast, fast, fastFirst),
            (IndicatorLines.Slow, slow, slowFirst),
        ]);
    }
}
