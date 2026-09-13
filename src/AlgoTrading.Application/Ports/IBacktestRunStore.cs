using AlgoTrading.Domain.Reporting;

namespace AlgoTrading.Application.Ports;

/// <summary>Résumé d'un run persisté, tel qu'on le liste ou le compare.</summary>
public sealed record BacktestRunSummary(
    int Id,
    string StrategyName,
    string Fingerprint,
    DateOnly From,
    DateOnly To,
    decimal InitialCash,
    decimal FinalEquity,
    decimal TotalReturn,
    decimal MaxDrawdown,
    decimal Calmar,
    int TradeCount,
    DateTimeOffset RanAt);

/// <summary>
/// Conserve les résultats de backtest. C'est ce qui fait que les résultats cessent de vivre
/// en commentaires dans le code : ils deviennent listables et comparables.
/// </summary>
public interface IBacktestRunStore
{
    Task<int> SaveAsync(BacktestResult result, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BacktestRunSummary>> ListAsync(int limit = 20, CancellationToken cancellationToken = default);

    Task<BacktestRunSummary?> GetAsync(int id, CancellationToken cancellationToken = default);
}
