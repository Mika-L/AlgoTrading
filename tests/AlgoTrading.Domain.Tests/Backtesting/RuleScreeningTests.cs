using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Backtesting;

public class RuleScreeningTests
{
    private static readonly BarSeries Bars = TestBars.Synthetic(500);

    /// <summary>
    /// Une grille Rsi de 3 périodes × 2 seuils de survente, et quatre Ema dont les deux modes
    /// forment deux familles distinctes.
    /// </summary>
    private static readonly IReadOnlyList<RuleConfig> Catalog = RuleCatalog.Parse("""
        [
          { "type": "Threshold", "indicator": "Rsi", "parameters": { "period": [7, 14, 21] }, "bullishBelow": [25, 30], "bearishAbove": 70 },
          { "type": "Crossover", "indicator": "Ema", "parameters": { "fast": [5, 9], "slow": 20 }, "mode": ["Cross", "Relative"] }
        ]
        """).Rules;

    private static ScreeningReport Run(int minimumTrades = 0) => new RuleScreening().Run(
        new OptimizationRequest { Catalog = Catalog, Universe = [Bars], MinimumTrades = minimumTrades },
        cancellationToken: TestContext.Current.CancellationToken);

    private static ScreenedVariant Rsi(ScreeningReport report, decimal period, decimal bullishBelow) =>
        report.Groups.Single(static g => g.Indicator == "Rsi").Variants
            .Single(v => v.Rule.Parameters["period"] == period && v.Rule.BullishBelow == bullishBelow);

    [Fact]
    public void should_play_every_variant_alone_and_group_them_by_indicator()
    {
        var report = Run();

        report.Evaluated.ShouldBe(10);
        report.Groups.Select(static g => g.Indicator).Order(StringComparer.Ordinal).ShouldBe(["Ema", "Rsi"]);
        report.Groups.SelectMany(static g => g.Variants).Select(static v => v.Rule).ShouldBe(Catalog, ignoreOrder: true);
    }

    [Fact]
    public void should_find_neighbours_one_step_away_on_a_single_setting()
    {
        var report = Run();

        // Un coin de la grille 3 × 2 a deux voisines, un bord du milieu en a trois.
        Rsi(report, 7m, 25m).Neighbours.ShouldBe(2);
        Rsi(report, 21m, 30m).Neighbours.ShouldBe(2);
        Rsi(report, 14m, 25m).Neighbours.ShouldBe(3);
    }

    [Fact]
    public void should_never_make_neighbours_of_variants_from_different_families()
    {
        // Cross et Relative ne sont pas deux crans d'une même grille : chaque Ema n'a pour
        // voisine que l'autre période de son propre mode.
        Run().Groups.Single(static g => g.Indicator == "Ema").Variants.ShouldAllBe(v => v.Neighbours == 1);
    }

    [Fact]
    public void should_score_a_variant_on_the_average_of_its_neighbourhood()
    {
        var report = Run();
        var centre = Rsi(report, 14m, 25m);
        var neighbourhood = new[] { centre, Rsi(report, 7m, 25m), Rsi(report, 21m, 25m), Rsi(report, 14m, 30m) };

        centre.NeighbourhoodScore.ShouldBe(neighbourhood.Average(static v => v.Score));
        centre.WorstNeighbour.ShouldBe(neighbourhood.Skip(1).Min(static v => v.Score));
    }

    [Fact]
    public void should_count_a_variant_that_trades_too_little_as_proving_nothing()
    {
        var variants = Run(minimumTrades: 1_000_000).Groups.SelectMany(static g => g.Variants).ToArray();

        variants.ShouldAllBe(v => !v.Eligible && v.NeighbourhoodScore == 0m && v.WorstNeighbour == 0m);
    }

    [Fact]
    public void should_rank_variants_from_the_strongest_neighbourhood_down()
    {
        foreach (var group in Run().Groups)
        {
            group.Variants.Select(static v => v.NeighbourhoodScore).ShouldBeInOrder(SortDirection.Descending);
        }
    }
}
