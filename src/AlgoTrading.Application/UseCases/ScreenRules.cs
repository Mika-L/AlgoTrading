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
}

public sealed class ScreenRulesHandler(IMarketDataRepository repository)
{
    public async Task<ScreeningReport> HandleAsync(
        ScreenRulesRequest request,
        IProgress<OptimizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tradable = await repository.LoadTradableAsync(request.Universe, request.From, request.To, cancellationToken).ConfigureAwait(false);

        return new RuleScreening().Run(
            new OptimizationRequest
            {
                Catalog = request.Catalog,
                Universe = tradable,
                From = request.From,
                To = request.To,
                MinimumTrades = request.MinimumTrades,
                InitialCash = request.InitialCash,
                Parallel = request.Parallel,
            },
            progress,
            cancellationToken);
    }
}
