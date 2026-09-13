using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.Indicators.Catalog;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Indicators;

public class RelativeStrengthIndexTests
{
    [Fact]
    public void should_measure_the_first_relative_strength_over_exactly_one_period_of_changes()
    {
        // Les 14 premières variations de la série valent, en centièmes :
        //   -25 +6 -54 +72 +50 +27 +32 +42 +24 -19 +14 -42 +67 0
        // Hausses : 6+72+50+27+32+42+24+14+67 = 334 → moyenne 3,34 / 14
        // Baisses  : 25+54+19+42            = 140 → moyenne 1,40 / 14 = 0,10
        // RS  = (3,34/14) / 0,10 = 2,385714…
        // RSI = 100 − 100 / (1 + RS) = 70,4641…
        var bars = TestBars.FromCloses(WilderReference.Closes);

        var result = new RelativeStrengthIndex(14).Compute(bars);

        result.TryGetValue(IndicatorLines.Value, 14, out var rsi).ShouldBeTrue();
        decimal.Round(rsi, 4).ShouldBe(70.4641m);
    }

    [Fact]
    public void should_then_smooth_the_relative_strength_the_wilder_way()
    {
        // 15ᵉ variation : 46,28 → 46,00, soit une baisse de 0,28.
        // Moyenne des hausses : 3,34/14 + (0     − 3,34/14) / 14 = 0,221531…
        // Moyenne des baisses : 0,10   + (0,28   − 0,10)   / 14 = 0,112857…
        // RSI = 100 − 100 / (1 + 0,221531/0,112857) = 66,2496…
        var bars = TestBars.FromCloses(WilderReference.Closes);

        var result = new RelativeStrengthIndex(14).Compute(bars);

        result.TryGetValue(IndicatorLines.Value, 15, out var rsi).ShouldBeTrue();
        decimal.Round(rsi, 4).ShouldBe(66.2496m);
    }

    [Fact]
    public void should_stay_silent_until_it_has_seen_a_full_period_of_changes()
    {
        var bars = TestBars.FromCloses(WilderReference.Closes);

        var result = new RelativeStrengthIndex(14).Compute(bars);

        result.TryGetValue(IndicatorLines.Value, 13, out _).ShouldBeFalse();
        result.TryGetValue(IndicatorLines.Value, 14, out _).ShouldBeTrue();
    }

    [Fact]
    public void should_saturate_at_a_hundred_when_the_price_never_falls()
    {
        var bars = TestBars.FromCloses([.. Enumerable.Range(1, 30).Select(i => (decimal)i)]);

        var result = new RelativeStrengthIndex(14).Compute(bars);

        result.TryGetValue(IndicatorLines.Value, 20, out var rsi).ShouldBeTrue();
        rsi.ShouldBe(100m);
    }

    [Fact]
    public void should_report_nothing_on_a_history_shorter_than_its_period()
    {
        var bars = TestBars.FromCloses(10m, 11m, 12m);

        var result = new RelativeStrengthIndex(14).Compute(bars);

        result.FirstValid.ShouldBe(bars.Count);
    }

    [Fact]
    public void should_refuse_a_period_that_is_not_strictly_positive()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new RelativeStrengthIndex(0));
    }
}
