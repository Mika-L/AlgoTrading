using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Application.UseCases;

public sealed record ScreenRulesRequest
{
    public required IReadOnlyList<RuleConfig> Catalog { get; init; }

    public IReadOnlyList<Symbol> Universe { get; init; } = [];

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public int MinimumTrades { get; init; }

    public decimal InitialCash { get; init; } = 100_000m;

    public bool Parallel { get; init; } = true;

    public RankingObjective Objective { get; init; } = RankingObjective.Calmar;

    /// <summary>Le nom du catalogue, pour le journal des explorations.</summary>
    public string? CatalogName { get; init; }
}

public sealed class ScreenRulesHandler(IMarketDataRepository repository, IExplorationJournal journal)
{
    public async Task<ScreeningReport> HandleAsync(
        ScreenRulesRequest request,
        IProgress<OptimizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var symbols = await repository.RequestedAsync(request.Universe, cancellationToken).ConfigureAwait(false);
        var tradable = await repository.LoadTradableAsync(symbols, request.From, request.To, cancellationToken).ConfigureAwait(false);
        var sessions = TradableUniverse.Sessions(tradable, request.From, request.To);
        var history = await journal.HistoryAsync(symbols, cancellationToken).ConfigureAwait(false);

        // L'exploration est synchrone et longue : elle ne doit pas occuper le fil de l'appelant.
        var report = await Task.Run(
            () => new RuleScreening().Run(
                new OptimizationRequest
                {
                    Catalog = request.Catalog,
                    Universe = tradable,
                    From = request.From,
                    To = request.To,
                    MinimumTrades = request.MinimumTrades,
                    InitialCash = request.InitialCash,
                    Parallel = request.Parallel,
                    Objective = request.Objective,
                    History = history.Overlapping(sessions.First, sessions.Last),
                },
                progress,
                cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var best = report.Groups.Count == 0 ? null : report.Groups[0].Variants[0];

        await journal.RecordAsync(
            [
                new ExplorationEntry(ExplorationKind.Screening, symbols, sessions.First, sessions.Last, report.Trials)
                {
                    Catalog = request.CatalogName,
                    Objective = request.Objective,
                    Best = best?.Label,
                    BestDeflatedSharpe = best?.DeflatedSharpe,
                },
            ],
            cancellationToken).ConfigureAwait(false);

        return report;
    }
}
