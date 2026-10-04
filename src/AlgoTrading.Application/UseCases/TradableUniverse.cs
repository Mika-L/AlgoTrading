using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Application.UseCases;

internal static class TradableUniverse
{
    /// <summary>
    /// Les séries cotées de l'univers demandé sur la plage — tous les instruments connus si
    /// aucun n'est désigné. Une plage sans aucune cotation est une erreur.
    /// </summary>
    public static async Task<BarSeries[]> LoadTradableAsync(
        this IMarketDataRepository repository,
        IReadOnlyList<Symbol> symbols,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (symbols.Count == 0)
        {
            var known = await repository.ListInstrumentsAsync(cancellationToken).ConfigureAwait(false);
            symbols = [.. known.Select(static i => i.Symbol)];
        }

        var universe = await repository.LoadUniverseAsync(symbols, from, to, cancellationToken).ConfigureAwait(false);
        var tradable = universe.Where(static s => !s.IsEmpty).ToArray();

        return tradable.Length == 0
            ? throw new InvalidOperationException("Aucune cotation dans la plage demandée.")
            : tradable;
    }
}
