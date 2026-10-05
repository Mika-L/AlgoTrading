using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Application.UseCases;

public sealed record RunWalkForwardRequest
{
    public required IReadOnlyList<RuleConfig> Catalog { get; init; }

    public IReadOnlyList<Symbol> Universe { get; init; } = [];

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public int TrainingMonths { get; init; } = 36;

    public int TestMonths { get; init; } = 12;

    public int MinimumRules { get; init; } = 2;

    public int MaximumRules { get; init; } = 4;

    public int MinimumTrades { get; init; }

    public long? SampleSize { get; init; }

    public ulong Seed { get; init; } = 1;

    public bool OneVariantPerIndicator { get; init; } = true;

    public decimal InitialCash { get; init; } = 100_000m;

    public bool Parallel { get; init; } = true;

    public RankingObjective Objective { get; init; } = RankingObjective.Calmar;

    public int? ScreenTopPerIndicator { get; init; }

    public bool RequireEdge { get; init; }

    /// <summary>Le nom du catalogue, pour le journal des explorations.</summary>
    public string? CatalogName { get; init; }
}

public sealed class RunWalkForwardHandler(IMarketDataRepository repository, IExplorationJournal journal)
{
    public async Task<WalkForwardReport> HandleAsync(
        RunWalkForwardRequest request,
        IProgress<WalkForwardProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var symbols = await repository.RequestedAsync(request.Universe, cancellationToken).ConfigureAwait(false);
        var tradable = await repository.LoadTradableAsync(symbols, request.From, request.To, cancellationToken).ConfigureAwait(false);
        var history = await journal.HistoryAsync(symbols, cancellationToken).ConfigureAwait(false);

        // L'exploration est synchrone et longue : elle ne doit pas occuper le fil de l'appelant.
        var report = await Task.Run(
            () => new WalkForward().Run(
                new WalkForwardRequest
                {
                    Optimization = new OptimizationRequest
                    {
                        Catalog = request.Catalog,
                        Universe = tradable,
                        MinimumRules = request.MinimumRules,
                        MaximumRules = request.MaximumRules,
                        MinimumTrades = request.MinimumTrades,
                        SampleSize = request.SampleSize,
                        Seed = request.Seed,
                        OneVariantPerIndicator = request.OneVariantPerIndicator,
                        InitialCash = request.InitialCash,
                        Parallel = request.Parallel,
                        Objective = request.Objective,
                        Top = 1,
                    },
                    From = request.From,
                    To = request.To,
                    TrainingMonths = request.TrainingMonths,
                    TestMonths = request.TestMonths,
                    ScreenTopPerIndicator = request.ScreenTopPerIndicator,
                    RequireEdge = request.RequireEdge,
                    History = history,
                },
                progress,
                cancellationToken),
            cancellationToken).ConfigureAwait(false);

        // Une entrée par fenêtre, enregistrées ensemble à la fin : les fenêtres d'un même
        // walk-forward ne se comptent pas entre elles.
        await journal.RecordAsync(
            [
                .. report.Windows
                    .Where(static w => w.Trials.Trials > 0)
                    .Select(w => new ExplorationEntry(ExplorationKind.WalkForwardWindow, symbols, w.TrainFrom, w.TrainTo, w.Trials)
                    {
                        Catalog = request.CatalogName,
                        Objective = request.Objective,
                        Best = w.Selected?.Strategy.Name,
                        BestDeflatedSharpe = w.DeflatedSharpe,
                    }),
            ],
            cancellationToken).ConfigureAwait(false);

        return report;
    }
}
