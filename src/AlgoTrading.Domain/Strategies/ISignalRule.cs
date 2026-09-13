using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Strategies;

/// <summary>
/// Tout ce qu'une règle reçoit pour se prononcer : les barres, le résultat de son indicateur,
/// et la position dans la série. Elle ne connaît ni date ni portefeuille.
/// </summary>
public readonly record struct RuleContext(BarSeries Bars, IndicatorResult Indicator, int BarIndex)
{
    public decimal Close => Bars.Close[BarIndex];

    public bool TryGetValue(string line, out decimal value) => Indicator.TryGetValue(line, BarIndex, out value);

    public bool TryGetPrevious(string line, out decimal value) => Indicator.TryGetValue(line, BarIndex - 1, out value);
}

/// <summary>
/// Traduit un résultat d'indicateur en <see cref="Signal"/>. C'est ici qu'atterrissent les
/// <c>IsBullish</c> / <c>IsBearish</c> qui encombraient le contrat d'indicateur, et les seuils
/// qui y étaient codés en dur.
/// </summary>
public interface ISignalRule
{
    /// <summary>Nom lisible, qui sert à expliquer un signal agrégé.</summary>
    string Name { get; }

    /// <summary>Indicateur dont la règle a besoin — ce qui permet de mutualiser son calcul.</summary>
    IndicatorDescriptor Indicator { get; }

    SignalKind Kind { get; }

    /// <summary>Nombre de barres avant que la règle puisse se prononcer.</summary>
    int WarmupBars { get; }

    Signal Evaluate(in RuleContext context);
}
