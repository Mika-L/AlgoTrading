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

    public int MinimumTrades { get; init; }

    public long? SampleSize { get; init; }

    public ulong Seed { get; init; } = 1;

    public bool OneVariantPerIndicator { get; init; } = true;

    /// <summary>Persiste les résultats retenus.</summary>
    public bool Save { get; init; }
}

public sealed record OptimizeStrategyResponse(OptimizationReport Report, IReadOnlyList<int> SavedRunIds);

public sealed class OptimizeStrategyHandler(IMarketDataRepository repository, IBacktestRunStore store)
{
    public async Task<OptimizeStrategyResponse> HandleAsync(
        OptimizeStrategyRequest request,
        IProgress<OptimizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tradable = await repository.LoadTradableAsync(request.Universe, request.From, request.To, cancellationToken).ConfigureAwait(false);

        var report = new StrategyOptimizer().Run(new OptimizationRequest
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
            MinimumTrades = request.MinimumTrades,
            SampleSize = request.SampleSize,
            Seed = request.Seed,
            OneVariantPerIndicator = request.OneVariantPerIndicator,
        }, progress, cancellationToken);

        var saved = new List<int>();

        if (request.Save)
        {
            // Le classement ne garde que les mesures : les retenues sont rejouées pour être
            // persistées avec leur courbe et leurs trades.
            foreach (var candidate in report.Top)
            {
                var result = new BacktestEngine().Run(new BacktestRequest
                {
                    Strategy = candidate.Strategy,
                    Universe = tradable,
                    InitialCash = request.InitialCash,
                    From = request.From,
                    To = request.To,
                });

                saved.Add(await store.SaveAsync(result, cancellationToken).ConfigureAwait(false));
            }
        }

        return new OptimizeStrategyResponse(report, saved);
    }
}
