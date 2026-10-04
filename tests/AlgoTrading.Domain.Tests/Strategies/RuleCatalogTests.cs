using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Strategies.Rules;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Strategies;

public class RuleCatalogTests
{
    [Fact]
    public void should_read_a_catalog_without_ranges_as_it_is()
    {
        var catalog = RuleCatalog.Parse("""
            [
              { "type": "Threshold", "indicator": "Rsi", "parameters": { "period": 14 }, "bullishBelow": 30, "bearishAbove": 70 },
              { "type": "Crossover", "indicator": "Ema", "parameters": { "fast": 9, "slow": 20 } }
            ]
            """);

        catalog.Rules.Count.ShouldBe(2);
        catalog.Discarded.ShouldBe(0);
        catalog.Rules[0].Parameters["period"].ShouldBe(14m);
        catalog.Rules[1].Type.ShouldBe(CrossoverRule.Type);
    }

    [Fact]
    public void should_expand_a_list_of_values_into_one_rule_per_value()
    {
        var catalog = RuleCatalog.Parse("""
            [{ "type": "Threshold", "indicator": "Rsi", "parameters": { "period": [7, 14, 21] }, "bullishBelow": 30, "bearishAbove": 70 }]
            """);

        catalog.Rules.Select(static r => r.Parameters["period"]).ShouldBe([7m, 14m, 21m]);
    }

    [Fact]
    public void should_expand_a_range_bounds_included()
    {
        var catalog = RuleCatalog.Parse("""
            [{ "type": "Sign", "indicator": "Momentum", "parameters": { "period": { "from": 5, "to": 30, "step": 5 } } }]
            """);

        catalog.Rules.Select(static r => r.Parameters["period"]).ShouldBe([5m, 10m, 15m, 20m, 25m, 30m]);
    }

    [Fact]
    public void should_step_a_range_by_one_when_no_step_is_given()
    {
        var catalog = RuleCatalog.Parse("""
            [{ "type": "Sign", "indicator": "Momentum", "parameters": { "period": { "from": 3, "to": 5 } } }]
            """);

        catalog.Rules.Select(static r => r.Parameters["period"]).ShouldBe([3m, 4m, 5m]);
    }

    [Fact]
    public void should_cross_every_axis_of_an_entry_parameters_and_thresholds_alike()
    {
        var catalog = RuleCatalog.Parse("""
            [{ "type": "Threshold", "indicator": "Rsi", "parameters": { "period": [7, 14] }, "bullishBelow": [25, 30], "bearishAbove": 70 }]
            """);

        catalog.Rules.Select(static r => (r.Parameters["period"], r.BullishBelow)).ShouldBe(
        [
            (7m, 25m),
            (7m, 30m),
            (14m, 25m),
            (14m, 30m),
        ]);
    }

    [Fact]
    public void should_discard_and_count_the_variants_a_rule_refuses()
    {
        // Sur une grille de seuils, survente et surachat finissent toujours par se croiser.
        var catalog = RuleCatalog.Parse("""
            [{ "type": "Threshold", "indicator": "Rsi", "bullishBelow": [30, 40], "bearishAbove": [35, 70] }]
            """);

        catalog.Rules.Select(static r => (r.BullishBelow, r.BearishAbove)).ShouldBe([(30m, 35m), (30m, 70m), (40m, 70m)]);
        catalog.Discarded.ShouldBe(1);
    }

    [Fact]
    public void should_reject_an_entry_none_of_whose_variants_is_valid()
    {
        var error = Should.Throw<ArgumentException>(() => RuleCatalog.Parse("""
            [{ "type": "Threshold", "indicator": "Rsi", "bullishBelow": [70, 80], "bearishAbove": 30 }]
            """));

        error.Message.ShouldContain("Aucune variante valide pour l'entrée 1");
    }

    [Fact]
    public void should_expand_lists_of_non_numeric_settings_too()
    {
        var catalog = RuleCatalog.Parse("""
            [{ "type": "Crossover", "indicator": "Ema", "mode": ["Cross", "Relative"] }]
            """);

        catalog.Rules.Select(static r => r.Mode).ShouldBe([CrossoverMode.Cross, CrossoverMode.Relative]);
    }

    [Fact]
    public void should_keep_a_single_copy_of_identical_variants()
    {
        var catalog = RuleCatalog.Parse("""
            [{ "type": "Sign", "indicator": "Momentum", "parameters": { "period": [10, 10] } }]
            """);

        catalog.Rules.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("""{ "from": 5, "to": 30, "step": 0 }""", "strictement positif")]
    [InlineData("""{ "from": 30, "to": 5 }""", "la borne haute précède la basse")]
    [InlineData("""{ "from": 1, "to": 1000, "step": 1 }""", "dépasse 500 valeurs")]
    [InlineData("""{ "to": 30 }""", "exige un nombre « from »")]
    [InlineData("""[]""", "est vide")]
    public void should_reject_a_malformed_range(string range, string expected)
    {
        var error = Should.Throw<ArgumentException>(() => RuleCatalog.Parse(
            $$"""[{ "type": "Sign", "indicator": "Momentum", "parameters": { "period": {{range}} } }]"""));

        error.Message.ShouldContain(expected);
    }
}
