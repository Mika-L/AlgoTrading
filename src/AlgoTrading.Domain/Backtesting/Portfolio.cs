using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Backtesting;

/// <summary>
/// Le portefeuille unique, fusion des deux classes d'origine — l'une mono-actif, l'autre
/// multi-actifs, avec deux comptabilités divergentes.
/// <para><see cref="Apply"/> <b>valide puis mute</b>. Le code d'origine faisait l'inverse :
/// <c>Cash -= …</c> puis <c>if (Cash &lt; 0) throw</c>, ce qui laissait un portefeuille
/// incohérent derrière chaque exception rattrapée.</para>
/// </summary>
public sealed class Portfolio
{
    private readonly SortedDictionary<Symbol, Position> _positions = [];

    public Portfolio(decimal initialCash)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCash);

        InitialCash = initialCash;
        Cash = initialCash;
    }

    public decimal InitialCash { get; }

    public decimal Cash { get; private set; }

    /// <summary>Lignes ouvertes, triées par symbole : l'itération est déterministe.</summary>
    public IReadOnlyDictionary<Symbol, Position> Positions => _positions;

    public Position PositionOf(Symbol symbol) =>
        _positions.TryGetValue(symbol, out var position) ? position : new Position(symbol);

    public bool Holds(Symbol symbol) => _positions.ContainsKey(symbol);

    /// <summary>Valeur totale : liquidités plus lignes valorisées aux prix fournis.</summary>
    public decimal Equity(IReadOnlyDictionary<Symbol, decimal> marks)
    {
        ArgumentNullException.ThrowIfNull(marks);

        var total = Cash;

        foreach (var (symbol, position) in _positions)
        {
            if (!marks.TryGetValue(symbol, out var price))
            {
                throw new InvalidOperationException($"Impossible de valoriser {symbol} : aucun prix de référence.");
            }

            total += position.MarketValue(price);
        }

        return total;
    }

    public decimal UnrealizedPnL(IReadOnlyDictionary<Symbol, decimal> marks)
    {
        var total = 0m;

        foreach (var (symbol, position) in _positions)
        {
            if (marks.TryGetValue(symbol, out var price))
            {
                total += position.UnrealizedPnL(price);
            }
        }

        return total;
    }

    /// <summary>
    /// Vérifie qu'une exécution est réalisable, sans rien modifier. C'est ce contrôle qui
    /// permet de refuser un ordre proprement au lieu de lever au milieu d'une mutation.
    /// </summary>
    public bool CanApply(Execution execution, out RejectionReason reason)
    {
        ArgumentNullException.ThrowIfNull(execution);

        if (execution.Side == OrderSide.Buy)
        {
            if (Cash + execution.CashFlow < 0m)
            {
                reason = RejectionReason.InsufficientCash;
                return false;
            }
        }
        else if (PositionOf(execution.Symbol).Quantity < execution.Quantity)
        {
            reason = RejectionReason.NoPosition;
            return false;
        }

        reason = default;
        return true;
    }

    public void Apply(Execution execution)
    {
        if (!CanApply(execution, out var reason))
        {
            throw new InvalidOperationException($"Exécution refusée sur {execution.Symbol} : {reason}.");
        }

        var position = PositionOf(execution.Symbol);

        var updated = execution.Side == OrderSide.Buy
            ? position.Buy(execution.Quantity, execution.Price)
            : position.Sell(execution.Quantity);

        Cash += execution.CashFlow;

        if (updated.IsOpen)
        {
            _positions[execution.Symbol] = updated;
        }
        else
        {
            _positions.Remove(execution.Symbol);
        }
    }
}
