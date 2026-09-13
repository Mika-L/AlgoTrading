using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Application.Ports;

/// <summary>Source externe de cotations : une place de marché, un export CSV.</summary>
public interface IMarketDataProvider
{
    /// <summary>Nom court, celui qu'on écrit sur la ligne de commande.</summary>
    string Name { get; }

    Task<IReadOnlyList<RawBar>> FetchAsync(Symbol symbol, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <summary>Composition d'un univers de titres, tenue en configuration et non dans le code.</summary>
public interface IUniverseCatalog
{
    IReadOnlyList<(Symbol Symbol, string Name)> Resolve(string universeName);

    IReadOnlyList<string> Names { get; }
}
