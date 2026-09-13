namespace AlgoTrading.Domain.Reporting;

/// <summary>Valeur du portefeuille à la clôture d'une séance.</summary>
public readonly record struct EquityPoint(DateOnly Date, decimal Equity, decimal Cash, decimal Invested)
{
    public decimal Exposure => Equity == 0m ? 0m : Invested / Equity;
}
