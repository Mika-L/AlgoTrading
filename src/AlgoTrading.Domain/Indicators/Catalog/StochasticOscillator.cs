using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Oscillateur stochastique : position de la clôture dans l'amplitude de la fenêtre (%K),
/// et sa moyenne mobile (%D). Les seuils 20 / 80 codés en dur partent dans la règle.
/// </summary>
public sealed class StochasticOscillator : IndicatorBase
{
    public const string Kind = "Stochastic";

    public StochasticOscillator(int period = 14, int signalPeriod = 3)
    {
        Period = RequirePositive(period, nameof(period));
        SignalPeriod = RequirePositive(signalPeriod, nameof(signalPeriod));
        Descriptor = IndicatorDescriptor.Of(Kind, ("period", period), ("signal", signalPeriod));
    }

    public int Period { get; }

    public int SignalPeriod { get; }

    public override IndicatorDescriptor Descriptor { get; }

    // La ligne de signal amorce après le %K : c'est elle qui fixe l'amorçage global.
    public override int WarmupBars => Period - 1 + SignalPeriod - 1;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var highest = Series.HighestHigh(bars.High, Period, out var firstValid);
        var lowest = Series.LowestLow(bars.Low, Period, out _);
        var close = bars.Close;

        var k = new decimal[bars.Count];
        for (var i = firstValid; i < bars.Count; i++)
        {
            var range = highest[i] - lowest[i];

            // Une fenêtre sans amplitude n'a pas de position relative : convention 0.
            k[i] = range == 0m ? 0m : 100m * (close[i] - lowest[i]) / range;
        }

        var d = Series.Sma(k, SignalPeriod, out var signalFirst, firstValid);

        return IndicatorResult.Create(Descriptor, bars.Count,
        [
            (IndicatorLines.Value, k, firstValid),
            (IndicatorLines.Signal, d, signalFirst),
        ]);
    }
}
