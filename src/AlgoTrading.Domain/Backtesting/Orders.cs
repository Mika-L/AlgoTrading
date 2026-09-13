using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Backtesting;

public enum OrderSide
{
    Buy,
    Sell,
}

/// <summary>Ce qui a déclenché une exécution — utile pour expliquer un relevé de trades.</summary>
public enum ExecutionReason
{
    Signal,
    StopLoss,
    TakeProfit,
}

public enum RejectionReason
{
    /// <summary>Les liquidités ne couvraient pas l'ordre, vérification faite <b>avant</b> toute mutation.</summary>
    InsufficientCash,

    /// <summary>Le titre n'a pas coté avant l'expiration de l'ordre.</summary>
    Expired,

    /// <summary>La position avait déjà été soldée entre-temps.</summary>
    NoPosition,
}

/// <summary>
/// Ordre émis à la clôture d'une séance, exécutable à l'ouverture de la suivante.
/// <para>C'est ce report d'une séance qui corrige le look-ahead d'exécution : le code
/// d'origine faisait réagir un indicateur à la clôture de D <b>et</b> acheter à cette même
/// clôture, ce qui est structurellement impossible et gonfle toute stratégie réactive.</para>
/// </summary>
public sealed record PendingOrder(
    Symbol Symbol,
    OrderSide Side,
    int Quantity,
    DateOnly SignalDate,
    decimal Strength)
{
    /// <summary>Nombre de séances déjà passées sans avoir pu exécuter l'ordre.</summary>
    public int DeferredSessions { get; init; }

    public PendingOrder Defer() => this with { DeferredSessions = DeferredSessions + 1 };
}

public sealed record Execution(
    DateOnly Date,
    Symbol Symbol,
    OrderSide Side,
    int Quantity,
    decimal Price,
    decimal Commission,
    ExecutionReason Reason)
{
    /// <summary>Montant échangé, commission exclue.</summary>
    public decimal Notional => Price * Quantity;

    /// <summary>Effet net sur les liquidités : négatif à l'achat, positif à la vente.</summary>
    public decimal CashFlow => Side == OrderSide.Buy ? -(Notional + Commission) : Notional - Commission;
}

public sealed record RejectedOrder(DateOnly Date, PendingOrder Order, RejectionReason Reason);

/// <summary>Candidat retenu par les règles avant toute décision d'allocation.</summary>
public readonly record struct Candidate(Symbol Symbol, decimal Strength, decimal ReferencePrice);
