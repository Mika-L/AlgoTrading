using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Domain.Backtesting;

/// <summary>
/// Frottements d'exécution : commission et glissement. Les deux sont <b>non nuls par
/// défaut</b> — un backtest sans frais sur quarante titres avec signal quotidien mesure une
/// stratégie qui n'existe pas.
/// </summary>
public interface ICostModel
{
    /// <summary>Commission due sur un montant échangé.</summary>
    decimal Commission(decimal notional);

    /// <summary>Prix réellement obtenu : on paie un peu plus cher à l'achat, un peu moins à la vente.</summary>
    decimal ExecutionPrice(decimal referencePrice, OrderSide side);
}

public sealed class ProportionalCostModel(ExecutionPolicy policy) : ICostModel
{
    private readonly ExecutionPolicy _policy = policy ?? throw new ArgumentNullException(nameof(policy));

    public decimal Commission(decimal notional)
    {
        var proportional = Math.Abs(notional) * _policy.CommissionRate;
        return Math.Max(proportional, _policy.MinimumCommission);
    }

    public decimal ExecutionPrice(decimal referencePrice, OrderSide side)
    {
        var drift = referencePrice * _policy.SlippageRate;
        return side == OrderSide.Buy ? referencePrice + drift : referencePrice - drift;
    }
}

/// <summary>
/// Marché sans frottement. Réservé au run de comparaison qui isole l'effet des corrections
/// métier de celui du refactor — jamais un défaut.
/// </summary>
public sealed class ZeroCostModel : ICostModel
{
    public static ZeroCostModel Instance { get; } = new();

    public decimal Commission(decimal notional) => 0m;

    public decimal ExecutionPrice(decimal referencePrice, OrderSide side) => referencePrice;
}
