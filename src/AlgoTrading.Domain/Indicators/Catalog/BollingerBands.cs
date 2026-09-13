using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Bandes de Bollinger : moyenne mobile simple encadrée par un multiple de l'écart type.
/// L'écart type est calculé en <see cref="decimal"/> de bout en bout, là où le code
/// d'origine passait par un <c>double</c> pour la racine carrée.
/// </summary>
public sealed class BollingerBands : IndicatorBase
{
    public const string Kind = "Bollinger";

    public BollingerBands(int period = 20, decimal multiplier = 2m)
    {
        Period = RequirePositive(period, nameof(period));
        Multiplier = RequireNonNegative(multiplier, nameof(multiplier));
        Descriptor = IndicatorDescriptor.Of(Kind, ("period", period), ("multiplier", multiplier));
    }

    public int Period { get; }

    public decimal Multiplier { get; }

    public override IndicatorDescriptor Descriptor { get; }

    public override int WarmupBars => Period - 1;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var close = bars.Close;

        var middle = Series.Sma(close, Period, out var firstValid);
        var deviation = Series.StdDev(close, Period, out _);

        var upper = new decimal[bars.Count];
        var lower = new decimal[bars.Count];

        for (var i = firstValid; i < bars.Count; i++)
        {
            var offset = Multiplier * deviation[i];
            upper[i] = middle[i] + offset;
            lower[i] = middle[i] - offset;
        }

        return IndicatorResult.Create(Descriptor, bars.Count,
        [
            (IndicatorLines.Middle, middle, firstValid),
            (IndicatorLines.Upper, upper, firstValid),
            (IndicatorLines.Lower, lower, firstValid),
        ]);
    }
}
