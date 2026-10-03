using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Backtesting;

/// <summary>
/// Mille séances ouvrées depuis le 2 janvier 2017 mènent à l'automne 2020 : avec deux ans
/// d'apprentissage et six mois de test, quatre fenêtres, la dernière tronquée.
/// </summary>
public class WalkForwardTests
{
    private static readonly BarSeries Bars = TestBars.Synthetic(1_000);

    private static readonly IReadOnlyList<RuleConfig> Catalog = RuleCatalog.Parse("""
        [
          { "type": "Threshold", "indicator": "Rsi", "parameters": { "period": [7, 14] }, "bullishBelow": 30, "bearishAbove": 70 },
          { "type": "Crossover", "indicator": "Ema", "parameters": { "fast": 9, "slow": 20 } },
          { "type": "Sign", "indicator": "Momentum", "parameters": { "period": [5, 10] } }
        ]
        """).Rules;

    private static WalkForwardRequest Request() => new()
    {
        Optimization = new OptimizationRequest
        {
            Catalog = Catalog,
            Universe = [Bars],
            MinimumRules = 1,
            MaximumRules = 2,
            Top = 1,
        },
        TrainingMonths = 24,
        TestMonths = 6,
    };

    private static WalkForwardReport Run(WalkForwardRequest request) =>
        new WalkForward().Run(request, cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public void should_slide_by_one_test_period_and_test_right_after_each_training_window()
    {
        var windows = Run(Request()).Windows;

        windows.Select(static w => w.TestFrom).ShouldBe([new(2019, 1, 2), new(2019, 7, 2), new(2020, 1, 2), new(2020, 7, 2)]);
        windows.ShouldAllBe(w => w.TestFrom == w.TrainTo.AddDays(1) && w.TrainFrom.AddMonths(24) == w.TestFrom);
        windows[^1].TestTo.ShouldBe(Bars.Dates[^1]);
    }

    [Fact]
    public void should_never_test_a_combination_on_sessions_it_was_trained_on()
    {
        foreach (var window in Run(Request()).Windows)
        {
            window.Test.ShouldNotBeNull();
            window.Test.EquityCurve[0].Date.ShouldBeGreaterThanOrEqualTo(window.TestFrom);
            window.Test.EquityCurve[^1].Date.ShouldBeLessThanOrEqualTo(window.TestTo);
            window.TestFrom.ShouldBeGreaterThan(window.TrainTo);
        }
    }

    [Fact]
    public void should_test_the_winner_of_the_training_window()
    {
        var request = Request();
        var window = Run(request).Windows[1];

        var training = new StrategyOptimizer().Run(
            request.Optimization with { From = window.TrainFrom, To = window.TrainTo },
            cancellationToken: TestContext.Current.CancellationToken);

        window.Selected.ShouldNotBeNull();
        window.Selected.Strategy.Fingerprint.ShouldBe(training.Top[0].Strategy.Fingerprint);
        window.Test!.Fingerprint.ShouldBe(window.Selected.Strategy.Fingerprint);
    }

    [Fact]
    public void should_chain_the_test_windows_each_starting_where_the_previous_one_ended()
    {
        var report = Run(Request());

        var compounded = report.Windows.Aggregate(1m, static (growth, w) => growth * (w.Test!.FinalEquity / w.Test.InitialCash));

        report.OutOfSampleCurve.Select(static p => p.Date).ShouldBeInOrder(SortDirection.Ascending);
        report.OutOfSampleCurve.Select(static p => p.Date).Distinct().Count().ShouldBe(report.OutOfSampleCurve.Count);
        report.OutOfSampleCurve[^1].Equity.ShouldBe(100_000m * compounded, 0.01m);
        report.OutOfSample.TradeCount.ShouldBe(report.Windows.Sum(static w => w.Test!.Metrics.TradeCount));
    }

    [Fact]
    public void should_measure_buy_and_hold_on_the_same_test_windows()
    {
        var report = Run(Request());

        foreach (var window in report.Windows)
        {
            var expected = BuyAndHold.Curve([Bars], window.TestFrom, window.TestTo, 100_000m);
            window.BenchmarkReturn.ShouldBe((expected[^1].Equity / 100_000m) - 1m);
        }

        var compounded = report.Windows.Aggregate(1m, static (growth, w) => growth * (1m + w.BenchmarkReturn));
        report.BenchmarkCurve.Select(static p => p.Date).ShouldBe(report.OutOfSampleCurve.Select(static p => p.Date));
        report.BenchmarkCurve[^1].Equity.ShouldBe(100_000m * compounded, 0.01m);
    }

    [Fact]
    public void should_stay_in_cash_through_a_test_window_without_an_eligible_combination()
    {
        var request = Request();
        var report = Run(request with { Optimization = request.Optimization with { MinimumTrades = 1_000_000 } });

        report.Windows.ShouldAllBe(w => w.Selected == null && w.Test == null);
        report.OutOfSampleCurve.ShouldNotBeEmpty();
        report.OutOfSampleCurve.ShouldAllBe(p => p.Equity == 100_000m);
        report.Efficiency.ShouldBeNull();
    }

    [Fact]
    public void should_count_the_combinations_tried_across_all_windows()
    {
        // Quatre fenêtres, chacune explorant 5 règles seules et 2·1 + 2·2 + 1·2 = 8 paires.
        Run(Request()).Evaluated.ShouldBe(4 * (5 + 8));
    }

    [Fact]
    public void should_refuse_a_range_too_short_for_one_training_window_and_its_test()
    {
        var error = Should.Throw<ArgumentException>(() => Run(Request() with { TrainingMonths = 48 }));

        error.Message.ShouldContain("ne contient pas une fenêtre d'apprentissage de 48 mois");
    }
}
