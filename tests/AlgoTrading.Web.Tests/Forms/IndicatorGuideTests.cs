using AlgoTrading.Domain.Indicators;
using AlgoTrading.Web.Forms;
using Shouldly;

namespace AlgoTrading.Web.Tests.Forms;

public class IndicatorGuideTests
{
    private static readonly IndicatorLineCatalog Catalog = IndicatorLineCatalog.Discover();

    [Fact]
    public void should_explain_every_indicator_of_the_domain_catalog_and_nothing_else()
    {
        IndicatorGuide.All.Select(static e => e.Kind).ShouldBe(IndicatorCatalog.Kinds, ignoreOrder: true);
    }

    [Fact]
    public void should_explain_exactly_the_parameters_of_each_indicator()
    {
        foreach (var indicator in Catalog.Indicators)
        {
            IndicatorGuide.Find(indicator.Kind)!.Parameters.Keys
                .ShouldBe(indicator.Defaults.Select(static p => p.Name), ignoreOrder: true, indicator.Kind);
        }
    }

    [Fact]
    public void should_explain_exactly_the_lines_each_indicator_produces()
    {
        foreach (var indicator in Catalog.Indicators)
        {
            IndicatorGuide.Find(indicator.Kind)!.Lines.Keys.ShouldBe(indicator.Lines, ignoreOrder: true, indicator.Kind);
        }
    }

    [Fact]
    public void should_leave_no_explanation_blank()
    {
        foreach (var explanation in IndicatorGuide.All)
        {
            string[] texts = [explanation.Name, explanation.Measures, explanation.Computation, explanation.Reading, .. explanation.Parameters.Values, .. explanation.Lines.Values];
            texts.ShouldAllBe(static t => !string.IsNullOrWhiteSpace(t), explanation.Kind);
        }
    }
}
