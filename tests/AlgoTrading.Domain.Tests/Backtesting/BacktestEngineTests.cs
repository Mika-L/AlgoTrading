using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Strategies.Rules;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Backtesting;

/// <summary>
/// Le moteur est éprouvé avec une règle dont le verdict est entièrement prévisible : le
/// signe du momentum sur une barre, c'est-à-dire « la clôture a monté depuis hier ».
/// Aucune ambiguïté ne vient de l'indicateur, tout ce qui est observé vient du moteur.
/// </summary>
public class BacktestEngineTests
{
    private static StrategyDefinition RisesAndFalls(decimal fraction = 1m) => new()
    {
        Name = "Momentum d'une séance",
        Entry = new SignalPolicy
        {
            Mode = AggregationMode.Consensus,
            Rules = [new RuleConfig { Type = SignRule.Type, Indicator = "Momentum", Parameters = new() { ["period"] = 1m } }],
        },
        Sizing = new PositionSizing { Value = fraction },
        Execution = ExecutionPolicy.Frictionless,
    };

    private static BacktestResultOf Run(StrategyDefinition strategy, decimal cash, params BarSeries[] universe) =>
        new(new BacktestEngine().Run(new BacktestRequest
        {
            Strategy = strategy,
            Universe = universe,
            InitialCash = cash,
            Costs = ZeroCostModel.Instance,
        }));

    private sealed record BacktestResultOf(Domain.Reporting.BacktestResult Result);

    [Fact]
    public void should_execute_a_signal_at_the_next_opening_never_at_the_closing_that_produced_it()
    {
        // Le look-ahead d'exécution : le code d'origine faisait réagir un indicateur à la
        // clôture de D et acheter à cette même clôture — impossible, et cela gonfle
        // mécaniquement toute stratégie réactive.
        var bars = TestBars.FromOhlc(
            (100m, 100m, 100m, 100m),
            (100m, 110m, 100m, 110m),   // la clôture monte : signal haussier ici
            (105m, 130m, 105m, 120m));  // exécution à cette ouverture, 105

        var result = Run(RisesAndFalls(), 10_000m, bars).Result;

        result.Executions.Count.ShouldBe(1);
        result.Executions[0].Date.ShouldBe(bars[2].Date);
        result.Executions[0].Price.ShouldBe(105m);
        result.Executions[0].Price.ShouldNotBe(bars[1].Close);
    }

    [Fact]
    public void should_give_the_same_equity_curve_whatever_the_order_the_universe_is_declared_in()
    {
        // Le biais d'ordre d'itération : le résultat dépendait de l'ordre d'énumération
        // d'un dictionnaire à clés par référence.
        var first = TestBars.FromCloses(Symbol.From("AAA.PA"), TestBars.Origin, 100m, 110m, 120m, 115m, 125m, 130m);
        var second = TestBars.FromCloses(Symbol.From("ZZZ.PA"), TestBars.Origin, 50m, 55m, 60m, 58m, 62m, 65m);

        var straight = Run(RisesAndFalls(0.5m), 10_000m, first, second).Result;
        var shuffled = Run(RisesAndFalls(0.5m), 10_000m, second, first).Result;

        straight.EquityCurve.ShouldBe(shuffled.EquityCurve);
        straight.FinalEquity.ShouldBe(shuffled.FinalEquity);
    }

    [Fact]
    public void should_share_the_cash_pro_rata_when_it_cannot_fund_every_candidate_of_the_day()
    {
        // Le code d'origine servait la première valeur de la boucle à hauteur de Cash × 0,1
        // avant même d'examiner les suivantes.
        var first = TestBars.FromCloses(Symbol.From("AAA.PA"), TestBars.Origin, 100m, 100m, 100m, 100m);
        var second = TestBars.FromCloses(Symbol.From("BBB.PA"), TestBars.Origin, 100m, 100m, 100m, 100m);

        // Les deux montent le même jour, chacun visant 60 % d'un portefeuille de 10 000 €.
        var rising = TestBars.FromCloses(Symbol.From("AAA.PA"), TestBars.Origin, 100m, 110m, 110m, 110m);
        var alsoRising = TestBars.FromCloses(Symbol.From("BBB.PA"), TestBars.Origin, 100m, 110m, 110m, 110m);

        var result = Run(RisesAndFalls(0.6m), 10_000m, rising, alsoRising).Result;

        var buys = result.Executions.Where(static e => e.Side == OrderSide.Buy).ToList();
        buys.Count.ShouldBe(2);

        // 2 × 6 000 € visés pour 10 000 € disponibles : chacun est réduit à 5 000 €.
        buys.Select(static b => b.Quantity).Distinct().Count().ShouldBe(1);
        buys.Sum(static b => b.Notional).ShouldBeLessThanOrEqualTo(10_000m);
        buys[0].Quantity.ShouldBe(45);   // 5 000 / 110 arrondi à l'entier inférieur
    }

    [Fact]
    public void should_balance_realised_and_unrealised_results_against_the_change_in_portfolio_value()
    {
        // L'invariant comptable : il attrape toute une classe d'erreurs de tenue de compte.
        var bars = TestBars.Synthetic(count: 120, seed: 4242);

        var result = Run(RisesAndFalls(0.5m), 50_000m, bars).Result;

        var realised = result.Trades.Sum(static t => t.NetPnL);
        var unrealised = result.OpenPositions.Values.Sum(t => t.UnrealizedPnL(bars[^1].Close));
        var openingCosts = result.Executions
            .Where(e => e.Side == OrderSide.Buy && result.OpenPositions.ContainsKey(e.Symbol))
            .Sum(static e => e.Commission);

        (realised + unrealised - openingCosts).ShouldBe(result.FinalEquity - result.InitialCash, 0.0000001m);
    }

    [Fact]
    public void should_never_let_the_cash_go_negative()
    {
        var bars = TestBars.Synthetic(count: 200, seed: 77);

        var result = Run(RisesAndFalls(0.9m), 20_000m, bars).Result;

        result.EquityCurve.ShouldAllBe(static point => point.Cash >= 0m);
    }

    [Fact]
    public void should_produce_the_very_same_run_from_a_strategy_that_went_through_its_serialized_form()
    {
        var bars = TestBars.Synthetic(count: 150, seed: 31);
        var original = RisesAndFalls(0.4m);
        var restored = StrategyDefinition.FromJson(original.ToJson());

        var before = Run(original, 25_000m, bars).Result;
        var after = Run(restored, 25_000m, bars).Result;

        after.Fingerprint.ShouldBe(before.Fingerprint);
        after.EquityCurve.ShouldBe(before.EquityCurve);
        after.Trades.Count.ShouldBe(before.Trades.Count);
    }

    [Fact]
    public void should_give_the_same_run_twice_in_a_row()
    {
        var bars = TestBars.Synthetic(count: 150, seed: 9);
        var strategy = RisesAndFalls(0.4m);

        var first = Run(strategy, 25_000m, bars).Result;
        var second = Run(strategy, 25_000m, bars).Result;

        second.EquityCurve.ShouldBe(first.EquityCurve);
        second.FinalEquity.ShouldBe(first.FinalEquity);
    }

    [Fact]
    public void should_carry_the_survivorship_caveat_with_the_figures_it_qualifies()
    {
        var result = Run(RisesAndFalls(), 10_000m, TestBars.Synthetic(count: 40)).Result;

        result.Caveats.ShouldContain(static c => c.Contains("survivant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void should_report_an_empty_run_rather_than_fail_when_no_session_falls_in_the_window()
    {
        var engine = new BacktestEngine();

        var result = engine.Run(new BacktestRequest
        {
            Strategy = RisesAndFalls(),
            Universe = [TestBars.Synthetic(count: 40)],
            From = new DateOnly(2030, 1, 1),
            To = new DateOnly(2030, 12, 31),
        });

        result.EquityCurve.ShouldBeEmpty();
        result.FinalEquity.ShouldBe(result.InitialCash);
    }

    [Fact]
    public void should_refuse_a_backtest_without_any_instrument()
    {
        var engine = new BacktestEngine();

        Should.Throw<ArgumentException>(() => engine.Run(new BacktestRequest { Strategy = RisesAndFalls(), Universe = [] }));
    }
}
