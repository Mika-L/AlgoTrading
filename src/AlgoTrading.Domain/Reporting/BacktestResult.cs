using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Reporting;

/// <summary>
/// Résultat complet et autoportant d'un backtest. Les résultats cessent ainsi de vivre en
/// commentaires dans le code : ils sont retournés, comparables, et persistables par l'appelant.
/// <para>Le moteur ne les écrit nulle part — il ne connaît ni base ni fichier.</para>
/// </summary>
public sealed record BacktestResult
{
    public required string StrategyName { get; init; }

    /// <summary>Empreinte de la stratégie : deux runs identiques portent la même.</summary>
    public required string Fingerprint { get; init; }

    public required DateOnly From { get; init; }

    public required DateOnly To { get; init; }

    public required decimal InitialCash { get; init; }

    public required decimal FinalEquity { get; init; }

    public required IReadOnlyList<Symbol> Universe { get; init; }

    public required IReadOnlyList<EquityPoint> EquityCurve { get; init; }

    public required IReadOnlyList<Trade> Trades { get; init; }

    public required IReadOnlyList<Execution> Executions { get; init; }

    public required IReadOnlyList<RejectedOrder> RejectedOrders { get; init; }

    public required IReadOnlyDictionary<Symbol, Position> OpenPositions { get; init; }

    public required PerformanceMetrics Metrics { get; init; }

    /// <summary>
    /// Réserves méthodologiques attachées au résultat — le biais du survivant d'un univers
    /// figé, par exemple. Mieux vaut les porter avec le chiffre que les oublier à côté.
    /// </summary>
    public IReadOnlyList<string> Caveats { get; init; } = [];
}
