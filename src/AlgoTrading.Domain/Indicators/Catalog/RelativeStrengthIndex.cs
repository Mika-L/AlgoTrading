using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Relative Strength Index de Wilder. Le lissage passe désormais par
/// <see cref="Series.WilderSmooth"/>, partagé avec l'ATR — le code d'origine avait deux
/// formules distinctes pour le même concept, dans le même dossier.
/// </summary>
public sealed class RelativeStrengthIndex : IndicatorBase
{
    public const string Kind = "Rsi";

    public RelativeStrengthIndex(int period = 14)
    {
        Period = RequirePositive(period, nameof(period));
        Descriptor = IndicatorDescriptor.Of(Kind, ("period", period));
    }

    public int Period { get; }

    public override IndicatorDescriptor Descriptor { get; }

    public override int WarmupBars => Period;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var count = bars.Count;
        if (count <= Period)
        {
            return Empty(count, IndicatorLines.Value);
        }

        var changes = Series.Delta(bars.Close, 1, out var changeFirst);

        var gains = new decimal[count];
        var losses = new decimal[count];
        for (var i = changeFirst; i < count; i++)
        {
            gains[i] = changes[i] > 0m ? changes[i] : 0m;
            losses[i] = changes[i] < 0m ? -changes[i] : 0m;
        }

        var averageGain = Series.WilderSmooth(gains, Period, out var firstValid, changeFirst);
        var averageLoss = Series.WilderSmooth(losses, Period, out _, changeFirst);

        var rsi = new decimal[count];
        for (var i = firstValid; i < count; i++)
        {
            // Sans aucune baisse sur la fenêtre, la force relative est infinie : le RSI sature à 100.
            rsi[i] = averageLoss[i] == 0m
                ? 100m
                : 100m - (100m / (1m + (averageGain[i] / averageLoss[i])));
        }

        return IndicatorResult.Single(Descriptor, rsi, firstValid);
    }
}
