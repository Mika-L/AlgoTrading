using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Canal de Keltner : moyenne mobile <b>exponentielle</b> encadrée par un multiple de l'ATR.
/// <para>Les deux composants du code d'origine étaient faux : la ligne médiane était une
/// moyenne arithmétique baptisée <c>ema</c>, et la largeur du canal une moyenne de
/// <c>High - Low</c> baptisée <c>atr</c> — elle ignorait donc les gaps d'ouverture.</para>
/// </summary>
public sealed class KeltnerChannel : IndicatorBase
{
    public const string Kind = "Keltner";

    public KeltnerChannel(int period = 20, decimal multiplier = 1.5m)
    {
        Period = RequirePositive(period, nameof(period));
        Multiplier = RequireNonNegative(multiplier, nameof(multiplier));
        Descriptor = IndicatorDescriptor.Of(Kind, ("period", period), ("multiplier", multiplier));
    }

    public int Period { get; }

    public decimal Multiplier { get; }

    public override IndicatorDescriptor Descriptor { get; }

    // L'ATR amorce une barre après l'EMA : la barre 0 n'a pas de true range.
    public override int WarmupBars => Period;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var middle = Series.Ema(bars.Close, Period, out var emaFirst);

        var trueRange = Series.TrueRange(bars.High, bars.Low, bars.Close, out var trueRangeFirst);
        var atr = Series.WilderSmooth(trueRange, Period, out var atrFirst, trueRangeFirst);

        var firstValid = Math.Max(emaFirst, atrFirst);
        var upper = new decimal[bars.Count];
        var lower = new decimal[bars.Count];

        for (var i = firstValid; i < bars.Count; i++)
        {
            var offset = Multiplier * atr[i];
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
