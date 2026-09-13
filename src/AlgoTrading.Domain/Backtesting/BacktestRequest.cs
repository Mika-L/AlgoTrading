using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Domain.Backtesting;

public sealed record BacktestRequest
{
    public required StrategyDefinition Strategy { get; init; }

    public required IReadOnlyList<BarSeries> Universe { get; init; }

    public decimal InitialCash { get; init; } = 100_000m;

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    /// <summary>
    /// Modèle de coûts. Laissé nul, il découle de la politique d'exécution de la stratégie ;
    /// on ne l'impose que pour le run de comparaison à frottement nul.
    /// </summary>
    public ICostModel? Costs { get; init; }

    public ICapitalAllocator Allocator { get; init; } = ProRataAllocator.Instance;
}
