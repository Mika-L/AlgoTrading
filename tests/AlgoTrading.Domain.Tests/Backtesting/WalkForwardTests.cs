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

    private static WalkForwardReport Run(WalkForwardRequest request, IProgress<WalkForwardProgress>? progress = null) =>
        new WalkForward().Run(request, progress, TestContext.Current.CancellationToken);

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
    public void should_screen_the_catalog_on_the_training_window_only()
    {
        var request = Request() with { ScreenTopPerIndicator = 1 };
        var report = Run(request);

        foreach (var window in report.Windows)
        {
            // Rejoué sur la seule fenêtre d'apprentissage, le criblage doit donner les mêmes scores :
            // rien de la fenêtre de test n'a pu peser sur le choix des règles.
            var alone = new RuleScreening().Run(
                request.Optimization with { From = window.TrainFrom, To = window.TrainTo },
                cancellationToken: TestContext.Current.CancellationToken);

            window.Screening.ShouldNotBeNull();
            window.Screening.Groups.SelectMany(static g => g.Variants).Select(static v => v.NeighbourhoodScore)
                .ShouldBe(alone.Groups.SelectMany(static g => g.Variants).Select(static v => v.NeighbourhoodScore));
            window.Catalog.ShouldBe(alone.Shortlist(1));
        }
    }

    [Fact]
    public void should_combine_only_the_rules_the_window_screening_kept()
    {
        foreach (var window in Run(Request() with { ScreenTopPerIndicator = 1 }).Windows.Where(static w => w.Selected is not null))
        {
            window.Selected!.Strategy.Entry.Rules.ShouldAllBe(rule => window.Catalog.Contains(rule));
        }
    }

    [Fact]
    public void should_count_the_screened_variants_among_the_strategies_tried()
    {
        var report = Run(Request() with { ScreenTopPerIndicator = 1 });

        // Les 5 variantes du catalogue sont criblées dans chacune des quatre fenêtres.
        report.Evaluated.ShouldBe((4 * 5) + report.Windows.Sum(static w => w.Training.Evaluated));
    }

    [Fact]
    public void should_deflate_each_winner_by_the_screened_variants_as_well_as_the_combinations()
    {
        var report = Run(Request() with { ScreenTopPerIndicator = 1 });
        report.Windows.ShouldContain(static w => w.Selected != null && w.Screening != null && w.Screening.Evaluated > 0);

        foreach (var window in report.Windows.Where(static w => w.Selected is not null))
        {
            // Le criblage a choisi les règles : ignorer ses essais placerait la barre trop bas.
            window.DeflatedSharpe!.Value.ShouldBeLessThanOrEqualTo(window.Training.DeflatedSharpe(window.Selected!));
        }

        report.OutOfSampleSharpeProbability.ShouldBeInRange(0m, 1m);
        report.BenchmarkSharpeProbability.ShouldBeInRange(0m, 1m);
    }

    [Fact]
    public void should_stay_in_cash_through_a_test_window_where_no_variant_passes_the_screening()
    {
        var request = Request() with { ScreenTopPerIndicator = 2 };
        var report = Run(request with { Optimization = request.Optimization with { MinimumTrades = 1_000_000 } });

        report.Windows.ShouldAllBe(w => w.Catalog.Count == 0 && w.Test == null && w.CashReason == "aucune variante ne passe le criblage");
    }

    [Fact]
    public void should_stay_in_cash_when_the_screening_keeps_too_few_rules_to_combine()
    {
        // Trois indicateurs au plus après criblage : aucune combinaison de quatre règles distinctes.
        var request = Request() with { ScreenTopPerIndicator = 1 };
        var report = Run(request with { Optimization = request.Optimization with { MinimumRules = 4, MaximumRules = 4 } });

        report.Windows.ShouldAllBe(w => w.Catalog.Count > 0 && w.Test == null && w.CashReason == "trop peu de règles retenues pour former une combinaison");
    }

    [Fact]
    public void should_report_screening_then_combining_for_each_window()
    {
        var stages = new List<(int Window, WalkForwardStage Stage)>();
        var request = Request() with { ScreenTopPerIndicator = 1 };

        Run(request with { Optimization = request.Optimization with { Parallel = false } }, new SynchronousProgress(p => stages.Add((p.Window, p.Stage))));

        stages.Distinct().ShouldBe(
        [
            .. Enumerable.Range(1, 4).SelectMany(static w => new[] { (w, WalkForwardStage.Screening), (w, WalkForwardStage.Combining) }),
        ]);
    }

    [Theory]
    [InlineData(RankingObjective.Calmar)]
    [InlineData(RankingObjective.InformationRatio)]
    public void should_only_play_a_winner_that_beat_the_market_in_training_when_an_edge_is_required(RankingObjective objective)
    {
        var request = Request();
        var report = Run(request with { RequireEdge = true, Optimization = request.Optimization with { Objective = objective } });

        foreach (var window in report.Windows)
        {
            window.Selected.ShouldNotBeNull();
            var edge = window.Selected.Score > window.Training.BenchmarkScore;

            (window.Test is not null).ShouldBe(edge);
            window.CashReason.ShouldBe(edge ? null : "aucune ne bat le marché en apprentissage");
        }
    }

    [Fact]
    public void should_play_the_winner_whatever_the_market_did_when_no_edge_is_required()
    {
        Run(Request()).Windows.ShouldAllBe(w => w.Test != null && w.CashReason == null);
    }

    [Fact]
    public void should_refuse_a_range_too_short_for_one_training_window_and_its_test()
    {
        var error = Should.Throw<ArgumentException>(() => Run(Request() with { TrainingMonths = 48 }));

        error.Message.ShouldContain("ne contient pas une fenêtre d'apprentissage de 48 mois");
    }

    private sealed class SynchronousProgress(Action<WalkForwardProgress> report) : IProgress<WalkForwardProgress>
    {
        public void Report(WalkForwardProgress value) => report(value);
    }
}
