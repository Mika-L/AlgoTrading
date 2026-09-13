using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Reporting;
using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Application.UseCases;

public sealed record RunBacktestRequest
{
    public required StrategyDefinition Strategy { get; init; }

    /// <summary>Titres à retenir. Vide, tout le référentiel est pris.</summary>
    public IReadOnlyList<Symbol> Universe { get; init; } = [];

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public decimal InitialCash { get; init; } = 100_000m;

    /// <summary>Persiste le résultat et retourne son identifiant.</summary>
    public bool Save { get; init; }

    /// <summary>
    /// Neutralise commissions et glissement. Réservé au run de comparaison qui sépare l'effet
    /// des corrections métier de celui du refactor.
    /// </summary>
    public bool Frictionless { get; init; }
}

public sealed record RunBacktestResponse(BacktestResult Result, int? RunId);

public sealed class RunBacktestHandler(IMarketDataRepository repository, IBacktestRunStore store)
{
    public async Task<RunBacktestResponse> HandleAsync(RunBacktestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Strategy.Validate();

        var symbols = request.Universe;

        if (symbols.Count == 0)
        {
            var known = await repository.ListInstrumentsAsync(cancellationToken).ConfigureAwait(false);
            symbols = [.. known.Select(static i => i.Symbol)];
        }

        if (symbols.Count == 0)
        {
            throw new InvalidOperationException("Aucun instrument en base : lancez d'abord « algo data fetch ».");
        }

        var universe = await repository.LoadUniverseAsync(symbols, request.From, request.To, cancellationToken).ConfigureAwait(false);
        var tradable = universe.Where(static s => !s.IsEmpty).ToArray();

        if (tradable.Length == 0)
        {
            throw new InvalidOperationException("Aucune cotation dans la plage demandée.");
        }

        var result = new BacktestEngine().Run(new BacktestRequest
        {
            Strategy = request.Strategy,
            Universe = tradable,
            InitialCash = request.InitialCash,
            From = request.From,
            To = request.To,
            Costs = request.Frictionless ? ZeroCostModel.Instance : null,
        });

        int? runId = request.Save
            ? await store.SaveAsync(result, cancellationToken).ConfigureAwait(false)
            : null;

        return new RunBacktestResponse(result, runId);
    }
}
