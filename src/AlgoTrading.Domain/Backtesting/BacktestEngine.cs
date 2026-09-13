using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Reporting;
using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Domain.Backtesting;

/// <summary>
/// Le moteur de backtest. Il est <b>pur</b> : ni contexte de base de données, ni chemin de
/// fichier, ni écriture. Il retourne un <see cref="BacktestResult"/> que l'appelant persiste.
/// <para>Chaque séance se déroule en quatre temps : exécution des ordres de la veille à
/// l'ouverture, contrôle du risque en séance, valorisation à la clôture, puis décision.
/// Cet ordre est ce qui rend le backtest honnête.</para>
/// <para>L'itération se fait sur les <b>séances</b> de l'univers via des curseurs monotones,
/// pas sur un calendrier civil : ~2 050 séances utiles au lieu de ~2 900 jours dont 40 %
/// sans cotation, et un accès indexé là où le code d'origine refaisait un
/// <c>Where().OrderByDescending().First()</c> sur tout l'historique.</para>
/// </summary>
public sealed class BacktestEngine
{
    public BacktestResult Run(BacktestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Strategy.Validate();

        if (request.Universe.Count == 0)
        {
            throw new ArgumentException("Un backtest a besoin d'au moins un titre.", nameof(request));
        }

        var strategy = request.Strategy;
        var costs = request.Costs ?? new ProportionalCostModel(strategy.Execution);

        // Ordre déterministe de l'univers : le résultat ne doit rien devoir à l'ordre de déclaration.
        var universe = request.Universe.OrderBy(static s => s.Symbol).ToArray();
        var calendar = TradingCalendar.FromSeries(universe).Slice(request.From, request.To);

        if (calendar.IsEmpty)
        {
            return EmptyResult(request, calendar);
        }

        var entry = strategy.Entry.ToAggregator();
        var exit = strategy.Exit?.ToAggregator();
        var cache = request.Cache ?? new IndicatorCache(universe);

        var entryResults = universe.ToDictionary(
            static s => s.Symbol,
            s => cache.ResultsFor(s.Symbol, entry.Indicators));

        var exitResults = exit is null
            ? []
            : universe.ToDictionary(static s => s.Symbol, s => cache.ResultsFor(s.Symbol, exit.Indicators));

        var portfolio = new Portfolio(request.InitialCash);
        var ledger = new TradeLedger();
        var equityCurve = new List<EquityPoint>(calendar.Count);
        var rejected = new List<RejectedOrder>();

        // Un curseur par titre, qui n'avance jamais que d'une barre par séance.
        var cursors = new int[universe.Length];
        var marks = new Dictionary<Symbol, decimal>(universe.Length);
        var pending = new List<PendingOrder>();

        for (var day = 0; day < calendar.Count; day++)
        {
            var date = calendar[day];

            AdvanceCursors(universe, cursors, marks, date);

            pending = ExecutePending(pending, universe, cursors, date, strategy, costs, portfolio, ledger, rejected);
            ApplyRisk(universe, cursors, date, strategy, costs, portfolio, ledger);

            var equity = portfolio.Equity(marks);
            equityCurve.Add(new EquityPoint(date, equity, portfolio.Cash, equity - portfolio.Cash));

            pending.AddRange(Decide(universe, cursors, date, strategy, entry, exit, entryResults, exitResults, portfolio, equity, costs, request.Allocator));
        }

        var finalEquity = equityCurve[^1].Equity;

        return new BacktestResult
        {
            StrategyName = strategy.Name,
            Fingerprint = strategy.Fingerprint,
            From = calendar.First,
            To = calendar.Last,
            InitialCash = request.InitialCash,
            FinalEquity = finalEquity,
            Universe = [.. universe.Select(static s => s.Symbol)],
            EquityCurve = equityCurve,
            Trades = ledger.Trades,
            Executions = ledger.Executions,
            RejectedOrders = rejected,
            OpenPositions = portfolio.Positions,
            Metrics = PerformanceCalculator.Calculate(equityCurve, ledger.Trades, request.InitialCash, ledger.TotalCosts),
            StrategyJson = strategy.ToJson(),
            Caveats = Caveats(strategy),
        };
    }

    /// <summary>Avance chaque curseur d'au plus une barre et rafraîchit les prix de valorisation.</summary>
    private static void AdvanceCursors(BarSeries[] universe, int[] cursors, Dictionary<Symbol, decimal> marks, DateOnly date)
    {
        for (var i = 0; i < universe.Length; i++)
        {
            var series = universe[i];
            var next = cursors[i];

            if (next < series.Count && series.Dates[next] == date)
            {
                marks[series.Symbol] = series.Close[next];
                cursors[i] = next + 1;
            }
        }
    }

    /// <summary>Index de la barre du jour pour ce titre, <c>-1</c> s'il n'a pas coté.</summary>
    private static int BarOfDay(BarSeries series, int cursor, DateOnly date) =>
        cursor > 0 && series.Dates[cursor - 1] == date ? cursor - 1 : -1;

    /// <summary>
    /// Phase 1 — les ordres émis la veille s'exécutent à l'<b>ouverture</b> du jour, glissement
    /// compris. Un titre qui n'a pas coté voit son ordre reporté, puis expiré.
    /// </summary>
    private static List<PendingOrder> ExecutePending(
        List<PendingOrder> pending,
        BarSeries[] universe,
        int[] cursors,
        DateOnly date,
        StrategyDefinition strategy,
        ICostModel costs,
        Portfolio portfolio,
        TradeLedger ledger,
        List<RejectedOrder> rejected)
    {
        if (pending.Count == 0)
        {
            return pending;
        }

        var carried = new List<PendingOrder>();

        foreach (var order in pending)
        {
            var index = Array.FindIndex(universe, s => s.Symbol == order.Symbol);
            var bar = BarOfDay(universe[index], cursors[index], date);

            if (bar < 0)
            {
                var deferred = order.Defer();

                if (deferred.DeferredSessions > strategy.Execution.OrderValidityDays)
                {
                    rejected.Add(new RejectedOrder(date, order, RejectionReason.Expired));
                }
                else
                {
                    carried.Add(deferred);
                }

                continue;
            }

            var price = costs.ExecutionPrice(universe[index].Open[bar], order.Side);
            var quantity = order.Side == OrderSide.Sell
                ? Math.Min(order.Quantity, portfolio.PositionOf(order.Symbol).Quantity)
                : order.Quantity;

            if (quantity <= 0)
            {
                rejected.Add(new RejectedOrder(date, order, RejectionReason.NoPosition));
                continue;
            }

            var execution = new Execution(date, order.Symbol, order.Side, quantity, price, costs.Commission(price * quantity), ExecutionReason.Signal);

            // Les liquidités sont vérifiées AVANT toute mutation du portefeuille.
            if (!portfolio.CanApply(execution, out var reason))
            {
                rejected.Add(new RejectedOrder(date, order, reason));
                continue;
            }

            portfolio.Apply(execution);
            ledger.Record(execution);
        }

        return carried;
    }

    /// <summary>
    /// Phase 2 — stop et prise de bénéfice se jouent sur le plus bas et le plus haut de la
    /// séance, donc <b>le jour même</b> : un stop à 2 % différé au lendemain ne veut rien dire.
    /// Un gap d'ouverture au-delà du seuil exécute à l'ouverture. Le stop prime.
    /// </summary>
    private static void ApplyRisk(
        BarSeries[] universe,
        int[] cursors,
        DateOnly date,
        StrategyDefinition strategy,
        ICostModel costs,
        Portfolio portfolio,
        TradeLedger ledger)
    {
        if (!strategy.Risk.IsActive || portfolio.Positions.Count == 0)
        {
            return;
        }

        foreach (var (symbol, position) in portfolio.Positions.ToArray())
        {
            var index = Array.FindIndex(universe, s => s.Symbol == symbol);
            var bar = BarOfDay(universe[index], cursors[index], date);

            if (bar < 0)
            {
                continue;
            }

            var series = universe[index];
            var entryPrice = position.AveragePrice;

            decimal? trigger = null;
            var reason = ExecutionReason.StopLoss;

            if (strategy.Risk.StopLoss is { } stop)
            {
                var level = entryPrice * (1m - stop);
                if (series.Low[bar] <= level)
                {
                    // Une ouverture déjà sous le seuil s'exécute à l'ouverture, pas au seuil.
                    trigger = Math.Min(level, series.Open[bar]);
                }
            }

            if (trigger is null && strategy.Risk.TakeProfit is { } target)
            {
                var level = entryPrice * (1m + target);
                if (series.High[bar] >= level)
                {
                    trigger = Math.Max(level, series.Open[bar]);
                    reason = ExecutionReason.TakeProfit;
                }
            }

            if (trigger is not { } raw)
            {
                continue;
            }

            var price = costs.ExecutionPrice(raw, OrderSide.Sell);
            var execution = new Execution(date, symbol, OrderSide.Sell, position.Quantity, price, costs.Commission(price * position.Quantity), reason);

            portfolio.Apply(execution);
            ledger.Record(execution);
        }
    }

    /// <summary>
    /// Phase 4 — sorties puis entrées. <b>Tous</b> les candidats sont collectés avant la
    /// moindre allocation, puis le capital est réparti en un bloc : plus de premier arrivé
    /// premier servi dicté par l'ordre d'énumération d'un dictionnaire.
    /// <para>Les ordres produits ici ne s'exécuteront qu'à l'ouverture de la séance suivante.</para>
    /// </summary>
    private static List<PendingOrder> Decide(
        BarSeries[] universe,
        int[] cursors,
        DateOnly date,
        StrategyDefinition strategy,
        SignalAggregator entry,
        SignalAggregator? exit,
        Dictionary<Symbol, Dictionary<string, IndicatorResult>> entryResults,
        Dictionary<Symbol, Dictionary<string, IndicatorResult>> exitResults,
        Portfolio portfolio,
        decimal equity,
        ICostModel costs,
        ICapitalAllocator allocator)
    {
        var orders = new List<PendingOrder>();
        var candidates = new List<Candidate>();

        for (var i = 0; i < universe.Length; i++)
        {
            var series = universe[i];
            var bar = BarOfDay(series, cursors[i], date);

            if (bar < 0)
            {
                continue;
            }

            var symbol = series.Symbol;
            var position = portfolio.PositionOf(symbol);

            if (position.IsOpen)
            {
                // Sans politique de sortie explicite, on sort sur le signal inverse de l'entrée.
                var policy = exit ?? entry;
                var results = exit is null ? entryResults[symbol] : exitResults[symbol];
                var signal = policy.Evaluate(series, results, bar);

                if (!signal.IsBearish)
                {
                    continue;
                }

                var quantity = strategy.Execution.ExitMode == ExitMode.CloseAll
                    ? position.Quantity
                    : Math.Max(1, (int)decimal.Ceiling(position.Quantity * strategy.Execution.ExitFraction));

                orders.Add(new PendingOrder(symbol, OrderSide.Sell, Math.Min(quantity, position.Quantity), date, signal.Strength));
                continue;
            }

            var entrySignal = entry.Evaluate(series, entryResults[symbol], bar);

            if (entrySignal.IsBullish)
            {
                candidates.Add(new Candidate(symbol, entrySignal.Strength, series.Close[bar]));
            }
        }

        foreach (var allocation in allocator.Allocate(candidates, equity, portfolio.Cash, strategy.Sizing, costs))
        {
            orders.Add(new PendingOrder(allocation.Symbol, OrderSide.Buy, allocation.Quantity, date, allocation.Strength));
        }

        return orders;
    }

    private static IReadOnlyList<string> Caveats(StrategyDefinition strategy)
    {
        var caveats = new List<string>
        {
            "Univers figé : appliquer la composition d'un indice d'aujourd'hui au passé surestime la performance (biais du survivant).",
        };

        if (!strategy.Risk.IsActive)
        {
            caveats.Add("Aucun stop de protection ni prise de bénéfice : les positions ne sont soldées que sur signal.");
        }

        return caveats;
    }

    private static BacktestResult EmptyResult(BacktestRequest request, TradingCalendar calendar) => new()
    {
        StrategyName = request.Strategy.Name,
        Fingerprint = request.Strategy.Fingerprint,
        From = request.From ?? default,
        To = request.To ?? default,
        InitialCash = request.InitialCash,
        FinalEquity = request.InitialCash,
        Universe = [.. request.Universe.Select(static s => s.Symbol).Order()],
        EquityCurve = [],
        Trades = [],
        Executions = [],
        RejectedOrders = [],
        OpenPositions = new Dictionary<Symbol, Position>(),
        Metrics = PerformanceMetrics.Empty,
        Caveats = ["Aucune séance dans la plage demandée."],
    };
}
