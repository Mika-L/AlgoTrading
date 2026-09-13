using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Williams %R : distance de la clôture au plus haut de la fenêtre, de −100 (survendu)
/// à 0 (suracheté). Les seuils −80 / −20 codés en dur partent dans la règle.
/// </summary>
public sealed class WilliamsPercentR : IndicatorBase
{
    public const string Kind = "WilliamsR";

    public WilliamsPercentR(int period = 14)
    {
        Period = RequirePositive(period, nameof(period));
        Descriptor = IndicatorDescriptor.Of(Kind, ("period", period));
    }

    public int Period { get; }

    public override IndicatorDescriptor Descriptor { get; }

    public override int WarmupBars => Period - 1;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var highest = Series.HighestHigh(bars.High, Period, out var firstValid);
        var lowest = Series.LowestLow(bars.Low, Period, out _);
        var close = bars.Close;

        var williams = new decimal[bars.Count];
        for (var i = firstValid; i < bars.Count; i++)
        {
            var range = highest[i] - lowest[i];
            williams[i] = range == 0m ? 0m : -100m * (highest[i] - close[i]) / range;
        }

        return IndicatorResult.Single(Descriptor, williams, firstValid);
    }
}
