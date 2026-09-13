using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Strategies.Rules;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Backtesting;

/// <summary>
/// Stop de protection et prise de bénéfice. La plomberie est livrée et éprouvée, mais reste
/// <b>inactive par défaut</b> : décision arbitrée, pour que le premier backtest après
/// refactor reste comparable à l'ancien.
/// </summary>
public class RiskPolicyTests
{
    private static StrategyDefinition Protected(decimal? stop, decimal? takeProfit) => new()
    {
        Name = "Momentum protégé",
        Entry = new SignalPolicy
        {
            Mode = AggregationMode.Consensus,
            Rules = [new RuleConfig { Type = SignRule.Type, Indicator = "Momentum", Parameters = new() { ["period"] = 1m } }],
        },
        Sizing = new PositionSizing { Value = 1m },
        Risk = new RiskPolicy { StopLoss = stop, TakeProfit = takeProfit },
        Execution = ExecutionPolicy.Frictionless,
    };

    private static Domain.Reporting.BacktestResult Run(StrategyDefinition strategy, BarSeries bars) =>
        new BacktestEngine().Run(new BacktestRequest
        {
            Strategy = strategy,
            Universe = [bars],
            InitialCash = 10_000m,
            Costs = ZeroCostModel.Instance,
        });

    /// <summary>Prix de revient après l'achat : l'ouverture de la 3ᵉ séance.</summary>
    private const decimal EntryPrice = 111m;

    /// <summary>Stop à 2 % du prix de revient.</summary>
    private const decimal StopLevel = 108.78m;

    /// <summary>Prise de bénéfice à 5 % du prix de revient.</summary>
    private const decimal TakeProfitLevel = 116.55m;

    /// <summary>
    /// Achat à 111 € à l'ouverture de la 3ᵉ séance, puis la séance à éprouver.
    /// <para>L'amplitude de la séance d'entrée est volontairement étroite : elle ne doit
    /// toucher ni le stop ni la prise de bénéfice, sans quoi c'est elle qu'on mesurerait.</para>
    /// </summary>
    private static BarSeries WithEntryThen((decimal Open, decimal High, decimal Low, decimal Close) day) =>
        TestBars.FromOhlc(
            (109m, 109m, 109m, 109m),
            (109m, 110m, 109m, 110m),        // la clôture monte : signal haussier
            (111m, 112m, 110.5m, 111.5m),    // achat à l'ouverture, 111 € ; la hausse continue
            day);

    [Fact]
    public void should_trigger_the_stop_on_the_low_of_the_session_not_on_its_close()
    {
        // Clôture à 105 €, bien au-dessus du stop : un stop lu sur la clôture ne verrait rien.
        var bars = WithEntryThen((112m, 114m, 105m, 113m));

        var result = Run(Protected(stop: 0.02m, takeProfit: null), bars);

        var stopped = result.Executions.Single(static e => e.Reason == ExecutionReason.StopLoss);
        stopped.Price.ShouldBe(StopLevel);
        stopped.Date.ShouldBe(bars[3].Date);
    }

    [Fact]
    public void should_execute_at_the_opening_when_the_session_gaps_below_the_stop()
    {
        // Ouverture à 100 €, déjà sous le stop de 108,78 € : impossible de sortir au stop.
        var bars = WithEntryThen((100m, 101m, 95m, 98m));

        var result = Run(Protected(stop: 0.02m, takeProfit: null), bars);

        var stopped = result.Executions.Single(static e => e.Reason == ExecutionReason.StopLoss);
        stopped.Price.ShouldBe(100m);
    }

    [Fact]
    public void should_let_the_stop_win_when_both_thresholds_are_touched_the_same_session()
    {
        var bars = WithEntryThen((111m, 118m, 105m, 114m));

        var result = Run(Protected(stop: 0.02m, takeProfit: 0.05m), bars);

        result.Executions.ShouldContain(static e => e.Reason == ExecutionReason.StopLoss);
        result.Executions.ShouldNotContain(static e => e.Reason == ExecutionReason.TakeProfit);
    }

    [Fact]
    public void should_take_the_profit_when_only_the_upper_threshold_is_reached()
    {
        var bars = WithEntryThen((113m, 118m, 112m, 117m));

        var result = Run(Protected(stop: 0.02m, takeProfit: 0.05m), bars);

        var taken = result.Executions.Single(static e => e.Reason == ExecutionReason.TakeProfit);
        taken.Price.ShouldBe(TakeProfitLevel);
    }

    [Fact]
    public void should_close_the_position_the_same_session_the_threshold_is_crossed()
    {
        // Un stop à 2 % différé au lendemain ne veut rien dire.
        var bars = WithEntryThen((112m, 114m, 105m, 113m));

        var result = Run(Protected(stop: 0.02m, takeProfit: null), bars);

        result.OpenPositions.ShouldBeEmpty();
    }

    [Fact]
    public void should_leave_the_position_alone_when_no_protection_is_configured()
    {
        var bars = WithEntryThen((112m, 114m, 105m, 113m));

        var result = Run(Protected(stop: null, takeProfit: null), bars);

        result.Executions.ShouldNotContain(static e => e.Reason != ExecutionReason.Signal);
    }

    [Fact]
    public void should_refuse_a_stop_expressed_outside_zero_and_one()
    {
        Should.Throw<InvalidOperationException>(() => Protected(stop: 1.5m, takeProfit: null).Validate());
        Should.Throw<InvalidOperationException>(() => Protected(stop: 0m, takeProfit: null).Validate());
    }
}
