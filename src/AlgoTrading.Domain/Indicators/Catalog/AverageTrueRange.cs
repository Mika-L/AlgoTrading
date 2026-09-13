using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Average True Range de Wilder.
/// <para>Deux corrections majeures par rapport au code d'origine : le lissage est celui de
/// Wilder et non une moyenne arithmétique ; et surtout l'ATR <b>cesse d'être directionnel</b>.
/// Il se comparait à <c>atr.Values.Average()</c>, moyenne de toute la série — futur compris.
/// C'était un look-ahead qui invalidait tout backtest incluant cet indicateur.</para>
/// <para>L'ATR mesure une volatilité, pas une direction : une règle de seuil peut s'en
/// servir, mais le seuil est alors explicite et connu d'avance.</para>
/// </summary>
public sealed class AverageTrueRange : IndicatorBase
{
    public const string Kind = "Atr";

    public AverageTrueRange(int period = 14)
    {
        Period = RequirePositive(period, nameof(period));
        Descriptor = IndicatorDescriptor.Of(Kind, ("period", period));
    }

    public int Period { get; }

    public override IndicatorDescriptor Descriptor { get; }

    public override int WarmupBars => Period;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var trueRange = Series.TrueRange(bars.High, bars.Low, bars.Close, out var trueRangeFirst);
        var atr = Series.WilderSmooth(trueRange, Period, out var firstValid, trueRangeFirst);

        return IndicatorResult.Single(Descriptor, atr, firstValid);
    }
}
