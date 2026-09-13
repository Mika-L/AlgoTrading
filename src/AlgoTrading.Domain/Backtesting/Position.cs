using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Backtesting;

/// <summary>
/// Ligne de portefeuille. Le prix de revient ne bouge <b>pas</b> à la vente — le code
/// d'origine lui soustrayait <c>prix × quantité</c> au lieu de <c>PRU × quantité</c>, ce qui
/// le faisait dériver à chaque cession — et il revient à zéro quand la ligne est soldée,
/// au lieu de provoquer une division par zéro.
/// </summary>
public sealed record Position
{
    public Position(Symbol symbol, int quantity = 0, decimal averagePrice = 0m)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(averagePrice);

        Symbol = symbol;
        Quantity = quantity;
        AveragePrice = quantity == 0 ? 0m : averagePrice;
    }

    public Symbol Symbol { get; }

    public int Quantity { get; }

    /// <summary>Prix de revient unitaire, nul quand la ligne est soldée.</summary>
    public decimal AveragePrice { get; }

    public bool IsOpen => Quantity > 0;

    public decimal CostBasis => Quantity * AveragePrice;

    public decimal MarketValue(decimal price) => Quantity * price;

    public decimal UnrealizedPnL(decimal price) => Quantity * (price - AveragePrice);

    public Position Buy(int quantity, decimal price)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);

        var total = Quantity + quantity;
        return new Position(Symbol, total, ((CostBasis + (quantity * price)) / total));
    }

    /// <summary>Cède une partie de la ligne. Le prix de revient des titres restants est inchangé.</summary>
    public Position Sell(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);

        if (quantity > Quantity)
        {
            throw new InvalidOperationException($"Impossible de céder {quantity} titres {Symbol} : la ligne n'en compte que {Quantity}.");
        }

        return new Position(Symbol, Quantity - quantity, AveragePrice);
    }

    public override string ToString() => $"{Symbol} × {Quantity} @ {AveragePrice:0.####}";
}
