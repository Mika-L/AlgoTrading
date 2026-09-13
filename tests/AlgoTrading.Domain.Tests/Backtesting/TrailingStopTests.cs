using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Strategies.Rules;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Backtesting;

/// <summary>
/// Stop suiveur en multiples d'ATR. Les séances d'amorçage de ces séries ont toutes la même
/// amplitude vraie — deux euros — si bien que l'ATR y vaut deux et que les niveaux se
/// calculent de tête : achat à 101 €, stop à 97 €, puis 99 €, puis 100 € à mesure que le
/// titre monte.
/// </summary>
public class TrailingStopTests
{
    private const decimal EntryPrice = 101m;

    /// <summary>Deux ATR de distance, soit quatre euros sur ces séries.</summary>
    private static TrailingAtrStop TwoAtr => new() { Multiple = 2m, Period = 2 };

    private static StrategyDefinition Protected(RiskPolicy risk) => new()
    {
        Name = "Momentum à stop suiveur",
        Entry = new SignalPolicy
        {
            Mode = AggregationMode.Consensus,
            Rules = [new RuleConfig { Type = SignRule.Type, Indicator = "Momentum", Parameters = new() { ["period"] = 1m } }],
        },
        Sizing = new PositionSizing { Value = 1m },
        Risk = risk,
        Execution = ExecutionPolicy.Frictionless,
    };

    private static StrategyDefinition Trailing(decimal? stopLoss = null, TrailingAtrStop? trailing = null) =>
        Protected(new RiskPolicy { StopLoss = stopLoss, TrailingAtr = trailing ?? TwoAtr });

    private static Domain.Reporting.BacktestResult Run(StrategyDefinition strategy, BarSeries bars) =>
        new BacktestEngine().Run(new BacktestRequest
        {
            Strategy = strategy,
            Universe = [bars],
            InitialCash = 10_000m,
            Costs = ZeroCostModel.Instance,
        });

    /// <summary>
    /// Deux séances d'amorçage, l'achat à 101 € à l'ouverture de la quatrième, deux séances
    /// de hausse qui portent le cliquet à 100 €, puis les séances à éprouver.
    /// </summary>
    private static BarSeries Climbing(params (decimal Open, decimal High, decimal Low, decimal Close)[] rest) =>
        TestBars.FromOhlc(
        [
            (100m, 101m, 99m, 100m),
            (100m, 101m, 99m, 100m),
            (100m, 101m, 99m, 101m),     // la clôture monte : signal haussier
            (101m, 103m, 101m, 103m),    // achat à l'ouverture, 101 € ; le cliquet s'ouvre à 97 € puis monte à 99 €
            (103m, 104m, 102m, 104m),    // la hausse continue : cliquet à 100 €
            .. rest,
        ]);

    [Fact]
    public void should_close_the_position_when_the_price_falls_two_atr_below_its_peak()
    {
        var bars = Climbing((104m, 104m, 99m, 100m));

        var result = Run(Trailing(), bars);

        var stopped = result.Executions.Single(static e => e.Reason == ExecutionReason.TrailingStop);
        stopped.Price.ShouldBe(100m);
        stopped.Date.ShouldBe(bars[5].Date);
    }

    [Fact]
    public void should_hold_the_position_while_the_fall_stays_inside_the_band()
    {
        // Un euro au-dessus du cliquet : la baisse reste du bruit.
        var bars = Climbing((104m, 104m, 101m, 102m));

        var result = Run(Trailing(), bars);

        result.Executions.ShouldNotContain(static e => e.Reason == ExecutionReason.TrailingStop);
        result.OpenPositions.ShouldNotBeEmpty();
    }

    [Fact]
    public void should_never_lower_the_stop_when_volatility_widens()
    {
        // La 6ᵉ séance quadruple l'amplitude : « plus haut − deux ATR » vaudrait 99 €, un euro
        // sous le cliquet. Un stop suiveur ne recule pas, et c'est bien 100 € qui sort.
        var bars = Climbing(
            (104m, 108m, 101m, 108m),
            (108m, 108m, 99.5m, 100m));

        var result = Run(Trailing(), bars);

        var stopped = result.Executions.Single(static e => e.Reason == ExecutionReason.TrailingStop);
        stopped.Price.ShouldBe(100m);
    }

    [Fact]
    public void should_execute_at_the_opening_when_the_session_gaps_below_the_trailing_stop()
    {
        // Ouverture à 95 €, déjà sous le cliquet de 100 € : impossible de sortir au niveau.
        var bars = Climbing((95m, 96m, 94m, 95m));

        var result = Run(Trailing(), bars);

        result.Executions.Single(static e => e.Reason == ExecutionReason.TrailingStop).Price.ShouldBe(95m);
    }

    [Fact]
    public void should_protect_the_entry_session_with_the_atr_known_before_it()
    {
        // La séance d'entrée s'effondre de 101 à 90 €, portant à elle seule l'ATR à 6,50 €.
        // Le stop la juge avec l'ATR de la veille — 2 € — donc sort à 97 €. Avec l'ATR du
        // jour, le niveau tomberait à 88 € et la chute passerait inaperçue.
        var bars = TestBars.FromOhlc(
            (100m, 101m, 99m, 100m),
            (100m, 101m, 99m, 100m),
            (100m, 101m, 99m, 101m),
            (101m, 101m, 90m, 95m));

        var result = Run(Trailing(), bars);

        var stopped = result.Executions.Single(static e => e.Reason == ExecutionReason.TrailingStop);
        stopped.Price.ShouldBe(EntryPrice - 4m);
        stopped.Date.ShouldBe(bars[3].Date);
    }

    [Fact]
    public void should_let_the_trailing_stop_win_over_a_looser_fixed_one()
    {
        // Stop fixe à 2 % de 101 € : 98,98 €, deux euros sous le cliquet.
        var bars = Climbing((104m, 104m, 99m, 100m));

        var result = Run(Trailing(stopLoss: 0.02m), bars);

        var stopped = result.Executions.Single(static e => e.Reason != ExecutionReason.Signal);
        stopped.Reason.ShouldBe(ExecutionReason.TrailingStop);
        stopped.Price.ShouldBe(100m);
    }

    [Fact]
    public void should_keep_the_fixed_stop_when_it_protects_better()
    {
        // Stop fixe à 0,5 % de 101 € : 100,495 €, au-dessus du cliquet.
        var bars = Climbing((104m, 104m, 99m, 100m));

        var result = Run(Trailing(stopLoss: 0.005m), bars);

        var stopped = result.Executions.Single(static e => e.Reason != ExecutionReason.Signal);
        stopped.Reason.ShouldBe(ExecutionReason.StopLoss);
        stopped.Price.ShouldBe(EntryPrice * 0.995m);
    }

    [Fact]
    public void should_stay_silent_until_the_atr_is_seeded()
    {
        // Quatorze séances d'amorçage pour une série qui en compte six : rien à opposer.
        var bars = Climbing((95m, 96m, 94m, 95m));

        var result = Run(Trailing(trailing: new TrailingAtrStop { Multiple = 2m }), bars);

        result.Executions.ShouldNotContain(static e => e.Reason == ExecutionReason.TrailingStop);
    }

    [Fact]
    public void should_open_the_next_position_at_its_own_entry_and_not_at_the_previous_peak()
    {
        // Sortie sur signal à 102 €, le titre retombe, rachat à 99 € : le cliquet de 100 €
        // hérité de la ligne précédente stopperait la nouvelle dès sa première séance.
        var bars = Climbing(
            (104m, 104m, 101m, 102m),    // la clôture baisse : vente à l'ouverture suivante
            (102m, 102m, 98m, 98m),      // vendu à 102 €, la baisse continue
            (98m, 99m, 97m, 99m),        // la clôture remonte : signal haussier
            (99m, 100m, 98m, 100m));     // racheté à 99 €, un euro sous l'ancien cliquet

        var result = Run(Trailing(), bars);

        result.Executions.ShouldNotContain(static e => e.Reason == ExecutionReason.TrailingStop);
        result.Executions.Count(static e => e.Side == OrderSide.Buy).ShouldBe(2);
        result.OpenPositions.ShouldNotBeEmpty();
    }

    [Fact]
    public void should_not_hand_the_previous_stop_to_a_line_reopened_the_next_day()
    {
        // La séance du stop clôture en hausse : elle sort à 100 € et rachète dans la foulée,
        // à l'ouverture de la suivante. Le cliquet de la ligne soldée y stopperait la nouvelle
        // avant qu'elle ait vécu une séance.
        var bars = Climbing(
            (104m, 106m, 99m, 106m),    // stop à 100 €, mais la clôture monte : rachat émis
            (95m, 96m, 93m, 95m));      // racheté à 95 € sur un trou d'ouverture

        var result = Run(Trailing(), bars);

        result.Executions.Count(static e => e.Reason == ExecutionReason.TrailingStop).ShouldBe(1);
        result.OpenPositions.ShouldNotBeEmpty();
    }

    [Fact]
    public void should_refuse_a_trailing_stop_that_is_not_a_positive_multiple()
    {
        Should.Throw<InvalidOperationException>(() => Trailing(trailing: new TrailingAtrStop { Multiple = 0m }).Validate());
        Should.Throw<InvalidOperationException>(() => Trailing(trailing: new TrailingAtrStop { Multiple = -1m }).Validate());
        Should.Throw<InvalidOperationException>(() => Trailing(trailing: new TrailingAtrStop { Multiple = 3m, Period = 0 }).Validate());
    }

    /// <summary>
    /// Achat à 100 €, stop à dix pour cent : 90 €. Le cours monte à 110 €, le stop suit à
    /// 99 €. Le titre ne bouge que de séance en séance, l'ATR n'a rien à voir ici.
    /// </summary>
    private static BarSeries RisingToOneHundredAndTen(params (decimal Open, decimal High, decimal Low, decimal Close)[] rest) =>
        TestBars.FromOhlc(
        [
            (100m, 100m, 100m, 100m),
            (100m, 101m, 99m, 101m),     // la clôture monte : signal haussier
            (100m, 110m, 100m, 110m),    // achat à l'ouverture, 100 € ; le plus haut porte le stop à 99 €
            .. rest,
        ]);

    [Fact]
    public void should_follow_the_peak_at_a_constant_distance()
    {
        var bars = RisingToOneHundredAndTen((110m, 110m, 98.9m, 99m));

        var result = Run(Protected(new RiskPolicy { TrailingRate = 0.1m }), bars);

        var stopped = result.Executions.Single(static e => e.Reason == ExecutionReason.TrailingStop);
        stopped.Price.ShouldBe(99m);
        stopped.Date.ShouldBe(bars[3].Date);
    }

    [Fact]
    public void should_leave_the_stop_where_the_peak_put_it_when_the_price_falls_back()
    {
        // Le repli à 105 € laisse le stop à 99 €. Adossé au cours du jour, il descendrait à
        // 94,50 € et la séance suivante passerait au travers.
        var bars = RisingToOneHundredAndTen(
            (110m, 110m, 105m, 110m),
            (110m, 110m, 98m, 100m));

        var result = Run(Protected(new RiskPolicy { TrailingRate = 0.1m }), bars);

        result.Executions.Single(static e => e.Reason == ExecutionReason.TrailingStop).Price.ShouldBe(99m);
    }

    [Fact]
    public void should_let_the_fraction_win_when_it_protects_better_than_the_multiple()
    {
        // Deux pour cent sous le plus haut de 104 € : 101,92 €, presque deux euros au-dessus
        // du cliquet que l'ATR pose à 100 €.
        var bars = Climbing((104m, 104m, 99m, 100m));

        var result = Run(Protected(new RiskPolicy { TrailingRate = 0.02m, TrailingAtr = TwoAtr }), bars);

        result.Executions.Single(static e => e.Reason == ExecutionReason.TrailingStop).Price.ShouldBe(101.92m);
    }

    [Fact]
    public void should_refuse_a_trailing_fraction_outside_zero_and_one()
    {
        Should.Throw<InvalidOperationException>(() => Protected(new RiskPolicy { TrailingRate = 0m }).Validate());
        Should.Throw<InvalidOperationException>(() => Protected(new RiskPolicy { TrailingRate = 10m }).Validate());
    }
}
