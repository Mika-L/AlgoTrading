using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Backtesting;

public class StrategyOptimizerTests
{
    private static readonly BarSeries Bars = TestBars.Synthetic(400);

    /// <summary>
    /// Quatre indicateurs, dont deux déclinés : trois Rsi et deux Momentum. Avec une variante
    /// par indicateur, les combinaisons de 2 et 3 règles sont 17 + 17 = 34 ; sans cette
    /// contrainte, C(7,2) + C(7,3) = 56.
    /// </summary>
    private static readonly IReadOnlyList<RuleConfig> Catalog = RuleCatalog.Parse("""
        [
          { "type": "Threshold", "indicator": "Rsi", "parameters": { "period": [7, 14, 21] }, "bullishBelow": 30, "bearishAbove": 70 },
          { "type": "Crossover", "indicator": "Ema", "parameters": { "fast": 9, "slow": 20 } },
          { "type": "Sign", "indicator": "Momentum", "parameters": { "period": [5, 10] } },
          { "type": "Threshold", "indicator": "Cci", "parameters": { "period": 20 }, "bullishBelow": -100, "bearishAbove": 100 }
        ]
        """).Rules;

    private static OptimizationRequest Request() => new()
    {
        Catalog = Catalog,
        Universe = [Bars],
        MinimumRules = 2,
        MaximumRules = 3,
        Top = 100,
    };

    private static OptimizationReport Run(OptimizationRequest request, IProgress<OptimizationProgress>? progress = null) =>
        new StrategyOptimizer().Run(request, progress, TestContext.Current.CancellationToken);

    [Fact]
    public void should_try_every_combination_once_with_one_variant_per_indicator()
    {
        var report = Run(Request());

        report.SearchSpace.ShouldBe(34);
        report.Evaluated.ShouldBe(34);
        report.Top.Select(static c => c.Strategy.Fingerprint).Distinct().Count().ShouldBe(34);
        report.Top.ShouldAllBe(c => c.Strategy.Entry.Rules.Select(r => r.Indicator).Distinct().Count() == c.Strategy.Entry.Rules.Count);
    }

    [Fact]
    public void should_combine_variants_of_the_same_indicator_when_allowed()
    {
        var report = Run(Request() with { OneVariantPerIndicator = false });

        report.SearchSpace.ShouldBe(56);
        report.Evaluated.ShouldBe(56);
    }

    [Fact]
    public void should_draw_the_requested_number_of_distinct_combinations()
    {
        var report = Run(Request() with { SampleSize = 10 });

        report.SearchSpace.ShouldBe(34);
        report.Evaluated.ShouldBe(10);
        report.Top.Select(static c => c.Strategy.Fingerprint).Distinct().Count().ShouldBe(10);
    }

    [Fact]
    public void should_draw_the_same_combinations_from_the_same_seed()
    {
        static string[] Drawn(ulong seed) =>
            [.. Run(Request() with { SampleSize = 10, Seed = seed }).Top.Select(static c => c.Strategy.Fingerprint).Order(StringComparer.Ordinal)];

        Drawn(42).ShouldBe(Drawn(42));
    }

    [Fact]
    public void should_explore_exhaustively_a_sample_larger_than_the_search_space()
    {
        var report = Run(Request() with { SampleSize = 1_000 });

        report.Evaluated.ShouldBe(34);
    }

    [Fact]
    public void should_keep_the_same_leaders_as_a_full_ranking()
    {
        // Le classement borné ne doit rien perdre : ses cinq premiers sont ceux d'un tri complet,
        // dans le même ordre, que l'exploration soit parallèle ou non.
        var full = Run(Request() with { Parallel = false }).Top.Take(5).Select(static c => c.Strategy.Fingerprint);
        var bounded = Run(Request() with { Top = 5 }).Top.Select(static c => c.Strategy.Fingerprint);

        bounded.ShouldBe(full);
    }

    [Fact]
    public void should_count_but_not_rank_the_combinations_that_trade_too_little()
    {
        var report = Run(Request() with { MinimumTrades = 1_000_000 });

        report.Evaluated.ShouldBe(34);
        report.Eligible.ShouldBe(0);
        report.Top.ShouldBeEmpty();
    }

    [Fact]
    public void should_report_progress_up_to_the_last_combination()
    {
        var reports = new List<OptimizationProgress>();

        Run(Request() with { Parallel = false }, new SynchronousProgress(reports.Add));

        reports.ShouldNotBeEmpty();
        reports[^1].Evaluated.ShouldBe(34);
        reports[^1].Planned.ShouldBe(34);
        reports[^1].Best.ShouldNotBeNull();
    }

    [Fact]
    public void should_return_an_empty_report_when_the_catalog_cannot_fill_a_combination()
    {
        var report = Run(Request() with { MinimumRules = 5, MaximumRules = 6 });

        report.Evaluated.ShouldBe(0);
        report.Top.ShouldBeEmpty();
    }

    private sealed class SynchronousProgress(Action<OptimizationProgress> report) : IProgress<OptimizationProgress>
    {
        public void Report(OptimizationProgress value) => report(value);
    }
}
