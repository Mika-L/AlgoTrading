namespace AlgoTrading.Infrastructure.Persistence;

/// <summary>Un instrument du référentiel.</summary>
public sealed class InstrumentRow
{
    public int Id { get; set; }

    public required string Symbol { get; set; }

    public required string Name { get; set; }

    public ICollection<DailyBarRow> Bars { get; set; } = [];
}

/// <summary>
/// Une séance telle qu'elle est stockée : prix <b>bruts</b>, plus la clôture ajustée.
/// Le rétro-ajustement se fait au chargement, une fois pour toutes.
/// </summary>
public sealed class DailyBarRow
{
    public long Id { get; set; }

    public int InstrumentId { get; set; }

    public InstrumentRow? Instrument { get; set; }

    public DateOnly Date { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public decimal AdjustedClose { get; set; }

    public long Volume { get; set; }
}

/// <summary>
/// Un backtest exécuté, avec la stratégie qui l'a produit. C'est ce qui rend les résultats
/// comparables d'un run à l'autre — ils vivaient jusqu'ici en commentaires dans le code.
/// </summary>
public sealed class BacktestRunRow
{
    public int Id { get; set; }

    public required string StrategyName { get; set; }

    public required string Fingerprint { get; set; }

    /// <summary>La stratégie sérialisée : un run reste rejouable à l'identique.</summary>
    public required string StrategyJson { get; set; }

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public decimal InitialCash { get; set; }

    public decimal FinalEquity { get; set; }

    public decimal TotalReturn { get; set; }

    public decimal AnnualisedReturn { get; set; }

    public decimal MaxDrawdown { get; set; }

    public decimal Calmar { get; set; }

    public decimal Sharpe { get; set; }

    public decimal WinRate { get; set; }

    public decimal TotalCosts { get; set; }

    public int TradeCount { get; set; }

    public required string Universe { get; set; }

    public DateTimeOffset RanAt { get; set; }
}
