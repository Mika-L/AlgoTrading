using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators;

/// <summary>
/// Un indicateur est une recette immuable et sans données : il ne sait pas dire si le marché
/// monte — c'est le travail d'une règle — et il ne sait pas lire un prix — c'est le travail
/// du moteur.
/// </summary>
public interface IIndicator
{
    IndicatorDescriptor Descriptor { get; }

    /// <summary>Nombre de barres nécessaires avant la première valeur signifiante.</summary>
    int WarmupBars { get; }

    /// <summary>Calcul pur, en une passe, sur des barres déjà rétro-ajustées.</summary>
    IndicatorResult Compute(BarSeries bars);
}
