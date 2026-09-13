using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Variation de la clôture sur un nombre de <b>barres</b>. Le code d'origine lisait la
/// clôture brute alors que tout le reste lisait l'ajustée ; le rétro-ajustement au
/// chargement supprime la question.
/// </summary>
public sealed class Momentum : IndicatorBase
{
    public const string Kind = "Momentum";

    public Momentum(int period = 10)
    {
        Period = RequirePositive(period, nameof(period));
        Descriptor = IndicatorDescriptor.Of(Kind, ("period", period));
    }

    public int Period { get; }

    public override IndicatorDescriptor Descriptor { get; }

    public override int WarmupBars => Period;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var values = Series.Delta(bars.Close, Period, out var firstValid);
        return IndicatorResult.Single(Descriptor, values, firstValid);
    }
}
