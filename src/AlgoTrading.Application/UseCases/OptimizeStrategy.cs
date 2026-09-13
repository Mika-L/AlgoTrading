using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Application.UseCases;

public sealed record OptimizeStrategyRequest
{
    public required IReadOnlyList<RuleConfig> Catalog { get; init; }

    public IReadOnlyList<Symbol> Universe { get; init; } = [];

    public int MinimumRules { get; init; } = 2;

    public int MaximumRules { get; init; } = 4;

    public int Top { get; init; } = 20;

    public decimal InitialCash { get; init; } = 100_000m;

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public bool Parallel { get; init; } = true;

    /// <summary>Persiste les résultats retenus.</summary>
    public bool Save { get; init; }
}

public sealed record OptimizeStrategyResponse(IReadOnlyList<OptimizationOutcome> Outcomes, IReadOnlyList<int> SavedRunIds);

public sealed class OptimizeStrategyHandler(IMarketDataRepository repository, IBacktestRunStore store)
{
    public async Task<OptimizeStrategyResponse> HandleAsync(OptimizeStrategyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var symbols = request.Universe;

        if (symbols.Count == 0)
        {
            var known = await repository.ListInstrumentsAsync(cancellationToken).ConfigureAwait(false);
            symbols = [.. known.Select(static i => i.Symbol)];
        }

        var universe = await repository.LoadUniverseAsync(symbols, request.From, request.To, cancellationToken).ConfigureAwait(false);
        var tradable = universe.Where(static s => !s.IsEmpty).ToArray();

        if (tradable.Length == 0)
        {
            throw new InvalidOperationException("Aucune cotation dans la plage demandée.");
        }

        var outcomes = new StrategyOptimizer().Run(new OptimizationRequest
        {
            Catalog = request.Catalog,
            Universe = tradable,
            MinimumRules = request.MinimumRules,
            MaximumRules = request.MaximumRules,
            InitialCash = request.InitialCash,
            From = request.From,
            To = request.To,
            Top = request.Top,
            Parallel = request.Parallel,
        }, cancellationToken);

        var saved = new List<int>();

        if (request.Save)
        {
            foreach (var outcome in outcomes)
            {
                saved.Add(await store.SaveAsync(outcome.Result, cancellationToken).ConfigureAwait(false));
            }
        }

        return new OptimizeStrategyResponse(outcomes, saved);
    }
}
