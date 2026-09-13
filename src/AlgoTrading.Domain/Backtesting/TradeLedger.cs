using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Backtesting;

/// <summary>Un aller-retour complet, apparié premier entré premier sorti.</summary>
public sealed record Trade(
    Symbol Symbol,
    DateOnly OpenedOn,
    DateOnly ClosedOn,
    int Quantity,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Costs,
    ExecutionReason ExitReason)
{
    /// <summary>Résultat net, frais d'entrée et de sortie déduits.</summary>
    public decimal NetPnL => (Quantity * (ExitPrice - EntryPrice)) - Costs;

    public bool IsWinner => NetPnL > 0m;

    public int HoldingDays => ClosedOn.DayNumber - OpenedOn.DayNumber;
}

/// <summary>
/// Tient le registre des exécutions et les apparie au fil de l'eau.
/// <para>Remplace la reconstruction a posteriori des trades, qui relisait les ordres en base
/// et <b>mutait des entités EF suivies</b> pour tenir son compte — la table des lignes de
/// portefeuille, elle, n'était jamais alimentée.</para>
/// </summary>
public sealed class TradeLedger
{
    private readonly Dictionary<Symbol, Queue<Lot>> _open = [];
    private readonly List<Execution> _executions = [];
    private readonly List<Trade> _trades = [];

    public IReadOnlyList<Execution> Executions => _executions;

    public IReadOnlyList<Trade> Trades => _trades;

    /// <summary>Résultat encaissé sur les allers-retours bouclés, frais compris.</summary>
    public decimal RealizedPnL { get; private set; }

    public decimal TotalCosts { get; private set; }

    public void Record(Execution execution)
    {
        ArgumentNullException.ThrowIfNull(execution);

        _executions.Add(execution);
        TotalCosts += execution.Commission;

        if (execution.Side == OrderSide.Buy)
        {
            Open(execution);
        }
        else
        {
            Close(execution);
        }
    }

    private void Open(Execution execution)
    {
        if (!_open.TryGetValue(execution.Symbol, out var lots))
        {
            lots = new Queue<Lot>();
            _open[execution.Symbol] = lots;
        }

        // La commission d'entrée est portée par le lot et suivra ses titres jusqu'à la sortie.
        lots.Enqueue(new Lot(execution.Date, execution.Quantity, execution.Price, execution.Commission));
    }

    private void Close(Execution execution)
    {
        if (!_open.TryGetValue(execution.Symbol, out var lots))
        {
            return;
        }

        var remaining = execution.Quantity;
        var exitCostPerShare = execution.Quantity > 0 ? execution.Commission / execution.Quantity : 0m;

        while (remaining > 0 && lots.Count > 0)
        {
            var lot = lots.Peek();
            var matched = Math.Min(remaining, lot.Remaining);

            var entryCostShare = lot.Quantity > 0 ? lot.Commission * matched / lot.Quantity : 0m;
            var costs = entryCostShare + (exitCostPerShare * matched);

            var trade = new Trade(
                execution.Symbol,
                lot.OpenedOn,
                execution.Date,
                matched,
                lot.Price,
                execution.Price,
                costs,
                execution.Reason);

            _trades.Add(trade);
            RealizedPnL += trade.NetPnL;

            remaining -= matched;

            if (lot.Remaining == matched)
            {
                lots.Dequeue();
            }
            else
            {
                lot.Remaining -= matched;
            }
        }

        if (lots.Count == 0)
        {
            _open.Remove(execution.Symbol);
        }
    }

    private sealed class Lot(DateOnly openedOn, int quantity, decimal price, decimal commission)
    {
        public DateOnly OpenedOn { get; } = openedOn;

        public int Quantity { get; } = quantity;

        public decimal Price { get; } = price;

        public decimal Commission { get; } = commission;

        public int Remaining { get; set; } = quantity;
    }
}
