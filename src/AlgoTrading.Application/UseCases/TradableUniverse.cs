using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Application.UseCases;

internal static class TradableUniverse
{
    /// <summary>
    /// Les titres demandés — tous les instruments connus si aucun n'est désigné —, qu'ils aient
    /// coté ou non sur la plage. C'est l'univers qu'une exploration interroge : un titre entré en
    /// bourse en cours de route n'en fait pas un autre.
    /// </summary>
    public static async Task<IReadOnlyList<Symbol>> RequestedAsync(
        this IMarketDataRepository repository,
        IReadOnlyList<Symbol> symbols,
        CancellationToken cancellationToken)
    {
        if (symbols.Count > 0)
        {
            return symbols;
        }

        var known = await repository.ListInstrumentsAsync(cancellationToken).ConfigureAwait(false);

        return [.. known.Select(static i => i.Symbol)];
    }

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
        symbols = await repository.RequestedAsync(symbols, cancellationToken).ConfigureAwait(false);

        var universe = await repository.LoadUniverseAsync(symbols, from, to, cancellationToken).ConfigureAwait(false);
        var tradable = universe.Where(static s => !s.IsEmpty).ToArray();

        return tradable.Length == 0
            ? throw new InvalidOperationException("Aucune cotation dans la plage demandée.")
            : tradable;
    }

    /// <summary>Les séances de l'univers effectivement couvertes par la plage demandée.</summary>
    public static TradingCalendar Sessions(BarSeries[] tradable, DateOnly? from, DateOnly? to)
    {
        var sessions = TradingCalendar.FromSeries(tradable).Slice(from, to);

        return sessions.IsEmpty ? throw new InvalidOperationException("Aucune séance dans la plage demandée.") : sessions;
    }
}
