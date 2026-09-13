using System.Collections.Concurrent;
using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Backtesting;

/// <summary>
/// Calcule chaque indicateur distinct <b>une seule fois par titre</b>, quel que soit le
/// nombre de combinaisons qui s'en servent.
/// <para>C'est ce que le descripteur rend possible : l'identité d'un calcul ne tient plus à
/// l'objet qui le porte. Le code d'origine recalculait l'intégralité des indicateurs d'une
/// combinaison rien que pour en lire le <c>GetType()</c>.</para>
/// <para>Sans état après remplissage, le cache est sûr à lire en parallèle.</para>
/// </summary>
public sealed class IndicatorCache
{
    private readonly ConcurrentDictionary<(Symbol Symbol, string DescriptorId), Lazy<IndicatorResult>> _entries = new();
    private readonly IReadOnlyDictionary<Symbol, BarSeries> _universe;

    public IndicatorCache(IEnumerable<BarSeries> universe)
    {
        ArgumentNullException.ThrowIfNull(universe);
        _universe = universe.ToDictionary(static s => s.Symbol);
    }

    public int Count => _entries.Count;

    public IndicatorResult Get(Symbol symbol, IndicatorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var lazy = _entries.GetOrAdd(
            (symbol, descriptor.Id),
            key => new Lazy<IndicatorResult>(() =>
            {
                if (!_universe.TryGetValue(key.Symbol, out var bars))
                {
                    throw new InvalidOperationException($"Aucun historique pour {key.Symbol}.");
                }

                return IndicatorCatalog.Create(descriptor).Compute(bars);
            }, LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    /// <summary>Les résultats dont une politique de signal a besoin sur un titre, indexés par identité.</summary>
    public Dictionary<string, IndicatorResult> ResultsFor(Symbol symbol, IReadOnlyList<IndicatorDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var results = new Dictionary<string, IndicatorResult>(descriptors.Count, StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            results[descriptor.Id] = Get(symbol, descriptor);
        }

        return results;
    }
}
