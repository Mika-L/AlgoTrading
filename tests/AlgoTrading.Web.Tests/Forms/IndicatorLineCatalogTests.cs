using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Strategies.Rules;
using AlgoTrading.Web.Forms;
using Shouldly;

namespace AlgoTrading.Web.Tests.Forms;

public class IndicatorLineCatalogTests
{
    private static readonly IndicatorLineCatalog Catalog = IndicatorLineCatalog.Discover();

    [Fact]
    public void should_know_every_indicator_of_the_domain_catalog()
    {
        Catalog.Indicators.Select(static i => i.Kind).ShouldBe(IndicatorCatalog.Kinds, ignoreOrder: true);
    }

    [Fact]
    public void should_discover_the_lines_each_indicator_produces()
    {
        Catalog.Find("Macd")!.Lines.ShouldBe([IndicatorLines.Value, IndicatorLines.Signal, IndicatorLines.Histogram], ignoreOrder: true);
        Catalog.Find("Bollinger")!.Lines.ShouldContain(IndicatorLines.Upper);
        Catalog.Find("Rsi")!.Lines.ShouldBe([IndicatorLines.Value]);
    }

    [Fact]
    public void should_offer_band_breakouts_only_on_indicators_with_bands()
    {
        Catalog.Indicators.Where(static i => Catalog.Supports(BandBreakoutRule.Type, i)).Select(static i => i.Kind).ShouldBe(["Bollinger", "Keltner"], ignoreOrder: true);
    }

    [Fact]
    public void should_build_a_valid_rule_for_every_indicator()
    {
        foreach (var indicator in Catalog.Indicators)
        {
            var rule = RuleForm.For(indicator);

            Should.NotThrow(() => RuleRegistry.Create(rule.ToConfig()), indicator.Kind);
            rule.Problems(Catalog).ShouldBeEmpty(indicator.Kind);
        }
    }
}
