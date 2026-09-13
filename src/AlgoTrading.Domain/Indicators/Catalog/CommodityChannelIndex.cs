using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Commodity Channel Index : écart du prix typique à sa moyenne, normalisé par l'écart
/// absolu moyen. Ex-<c>CCI</c>. Les seuils ±100 codés en dur partent dans la règle.
/// </summary>
public sealed class CommodityChannelIndex : IndicatorBase
{
    public const string Kind = "Cci";

    /// <summary>Constante de Lambert, qui cale ~70 à 80 % des valeurs dans la bande ±100.</summary>
    private const decimal LambertConstant = 0.015m;

    public CommodityChannelIndex(int period = 20)
    {
        Period = RequirePositive(period, nameof(period));
        Descriptor = IndicatorDescriptor.Of(Kind, ("period", period));
    }

    public int Period { get; }

    public override IndicatorDescriptor Descriptor { get; }

    public override int WarmupBars => Period - 1;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var typical = bars.TypicalPrices();

        var average = Series.Sma(typical, Period, out var firstValid);
        var deviation = Series.MeanAbsoluteDeviation(typical, Period, out _);

        var cci = new decimal[bars.Count];
        for (var i = firstValid; i < bars.Count; i++)
        {
            // Une fenêtre plate a un écart moyen nul : le prix ne s'écarte de rien.
            cci[i] = deviation[i] == 0m
                ? 0m
                : (typical[i] - average[i]) / (LambertConstant * deviation[i]);
        }

        return IndicatorResult.Single(Descriptor, cci, firstValid);
    }
}
