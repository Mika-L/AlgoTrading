using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.Reporting;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Strategies.Rules;
using AlgoTrading.Web.Forms;
using Shouldly;

namespace AlgoTrading.Web.Tests.Forms;

public class ScreeningHeatmapTests
{
    private const string Period = "parameters.period";

    private static ScreenedVariant Variant(decimal period, decimal bullishBelow, decimal bearishAbove, decimal calmar, bool eligible = true, string family = "rsi")
    {
        var rule = new RuleConfig
        {
            Type = ThresholdRule.Type,
            Indicator = "Rsi",
            Parameters = new() { ["period"] = period },
            BullishBelow = bullishBelow,
            BearishAbove = bearishAbove,
        };

        var strategy = new StrategyDefinition { Name = "Rsi", Entry = new SignalPolicy { Rules = [rule] } };
        var axes = new Dictionary<string, decimal> { [Period] = period, ["BullishBelow"] = bullishBelow, ["BearishAbove"] = bearishAbove, ["Weight"] = 1m };

        return new ScreenedVariant(strategy, family, axes, "Rsi", PerformanceMetrics.Empty with { Calmar = calmar }, eligible, 0, calmar, null);
    }

    [Fact]
    public void should_keep_in_each_cell_the_best_variant_whatever_its_other_settings()
    {
        // Deux seuils hauts pour chaque case période × seuil bas : la case garde le meilleur.
        var map = ScreeningHeatmap.For(
        [
            Variant(14, 30, 70, 0.40m),
            Variant(14, 30, 80, 0.10m),
            Variant(7, 30, 70, 0.20m),
            Variant(7, 30, 80, 0.25m),
            Variant(14, 25, 70, -0.10m),
            Variant(7, 25, 80, 0.05m),
        ], xAxis: Period, yAxis: "BullishBelow")!;

        map.Columns.ShouldBe([7m, 14m]);
        map.Rows.ShouldBe([25m, 30m]);
        map.Cells[1, 1].ShouldBe(0.40m);
        map.Cells[1, 0].ShouldBe(0.25m);
        map.Cells[0, 1].ShouldBe(-0.10m);
        map.Cells[0, 0].ShouldBe(0.05m);
        map.IsLeader(1, 1).ShouldBeTrue();
        map.IsLeader(1, 0).ShouldBeFalse();
    }

    [Fact]
    public void should_count_a_variant_that_traded_too_little_as_zero()
    {
        var map = ScreeningHeatmap.For([Variant(14, 30, 70, 0.30m), Variant(7, 30, 70, 0.90m, eligible: false)])!;

        map.Cells[0, 0].ShouldBe(0m);
    }

    [Fact]
    public void should_draw_a_single_row_when_only_one_setting_varies()
    {
        var map = ScreeningHeatmap.For([Variant(7, 30, 70, 0.1m), Variant(14, 30, 70, 0.2m), Variant(21, 30, 70, 0.3m)])!;

        map.XAxis.ShouldBe(Period);
        map.YAxis.ShouldBeNull();
        map.Rows.ShouldBe([(decimal?)null]);
        map.Columns.Count.ShouldBe(3);
    }

    [Fact]
    public void should_draw_nothing_when_no_setting_varies()
    {
        ScreeningHeatmap.For([Variant(14, 30, 70, 0.2m)]).ShouldBeNull();
    }

    [Fact]
    public void should_map_only_the_family_of_the_leading_variant()
    {
        var map = ScreeningHeatmap.For([Variant(14, 30, 70, 0.4m), Variant(7, 30, 70, 0.3m), Variant(21, 30, 70, 0.9m, family: "other")])!;

        map.Columns.ShouldBe([7m, 14m]);
    }

    [Theory]
    [InlineData(Period, "period")]
    [InlineData("BullishBelow", "seuil bas")]
    [InlineData("BearishAbove", "seuil haut")]
    public void should_name_settings_readably(string axis, string expected)
    {
        ScreeningHeatmap.AxisLabel(axis).ShouldBe(expected);
    }
}
