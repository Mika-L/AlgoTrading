using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Strategies.Rules;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Strategies;

public class SignalRuleTests
{
    private static readonly IndicatorDescriptor AnyEma = IndicatorDescriptor.Of("Ema", ("fast", 9), ("slow", 20));
    private static readonly IndicatorDescriptor AnyRsi = IndicatorDescriptor.Of("Rsi", ("period", 14));

    [Fact]
    public void should_still_see_a_friday_crossover_when_the_next_session_is_a_monday()
    {
        // La non-régression du bug du lundi. Le code d'origine lisait « hier » par
        // date.AddDays(-1) : un lundi, la veille est un dimanche, sans cotation — aucun
        // croisement n'était jamais détecté un lundi, soit ~20 % des signaux perdus.
        var bars = TestBars.FromCloses(10m, 10m, 10m, 10m, 10m, 10m);
        bars[4].Date.DayOfWeek.ShouldBe(DayOfWeek.Friday);
        bars[5].Date.DayOfWeek.ShouldBe(DayOfWeek.Monday);

        // La rapide passe au-dessus de la lente exactement à la barre du lundi.
        var result = IndicatorResult.Create(AnyEma, bars.Count,
        [
            (IndicatorLines.Fast, [9m, 9m, 9m, 9m, 9m, 11m], 0),
            (IndicatorLines.Slow, [10m, 10m, 10m, 10m, 10m, 10m], 0),
        ]);

        var rule = new CrossoverRule(AnyEma);

        rule.Evaluate(new RuleContext(bars, result, 5)).IsBullish.ShouldBeTrue();
    }

    [Fact]
    public void should_only_speak_on_the_bar_where_the_averages_actually_cross()
    {
        var bars = TestBars.FromCloses(10m, 10m, 10m, 10m);
        var result = IndicatorResult.Create(AnyEma, bars.Count,
        [
            (IndicatorLines.Fast, [9m, 11m, 12m, 13m], 0),
            (IndicatorLines.Slow, [10m, 10m, 10m, 10m], 0),
        ]);

        var rule = new CrossoverRule(AnyEma);

        rule.Kind.ShouldBe(SignalKind.Event);
        rule.Evaluate(new RuleContext(bars, result, 1)).IsBullish.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 2)).IsNeutral.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 3)).IsNeutral.ShouldBeTrue();
    }

    [Fact]
    public void should_speak_every_day_when_the_crossover_rule_reads_a_lasting_state()
    {
        var bars = TestBars.FromCloses(10m, 10m, 10m, 10m);
        var result = IndicatorResult.Create(AnyEma, bars.Count,
        [
            (IndicatorLines.Fast, [9m, 11m, 12m, 13m], 0),
            (IndicatorLines.Slow, [10m, 10m, 10m, 10m], 0),
        ]);

        var rule = new CrossoverRule(AnyEma, mode: CrossoverMode.Relative);

        rule.Kind.ShouldBe(SignalKind.State);
        rule.Evaluate(new RuleContext(bars, result, 0)).IsBearish.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 2)).IsBullish.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 3)).IsBullish.ShouldBeTrue();
    }

    [Fact]
    public void should_turn_a_lasting_state_into_a_single_event_when_wrapped()
    {
        var bars = TestBars.FromCloses(10m, 10m, 10m, 10m);
        var result = IndicatorResult.Create(AnyEma, bars.Count,
        [
            (IndicatorLines.Fast, [9m, 11m, 12m, 13m], 0),
            (IndicatorLines.Slow, [10m, 10m, 10m, 10m], 0),
        ]);

        var rule = EdgeRule.Wrap(new CrossoverRule(AnyEma, mode: CrossoverMode.Relative));

        rule.Kind.ShouldBe(SignalKind.Event);
        rule.Evaluate(new RuleContext(bars, result, 1)).IsBullish.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 2)).IsNeutral.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 3)).IsNeutral.ShouldBeTrue();
    }

    [Fact]
    public void should_leave_an_event_rule_untouched_when_wrapping_it()
    {
        var inner = new CrossoverRule(AnyEma);

        EdgeRule.Wrap(inner).ShouldBeSameAs(inner);
    }

    [Fact]
    public void should_read_an_oversold_oscillator_as_an_opportunity_to_buy()
    {
        var bars = TestBars.FromCloses(10m, 10m, 10m);
        var result = IndicatorResult.Single(AnyRsi, [25m, 50m, 80m], 0);

        var rule = new ThresholdRule(AnyRsi, bullishBelow: 30m, bearishAbove: 70m);

        rule.Evaluate(new RuleContext(bars, result, 0)).IsBullish.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 1)).IsNeutral.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 2)).IsBearish.ShouldBeTrue();
    }

    [Fact]
    public void should_grow_its_conviction_with_the_size_of_the_excess()
    {
        var bars = TestBars.FromCloses(10m, 10m);
        var result = IndicatorResult.Single(AnyRsi, [29m, 5m], 0);

        var rule = new ThresholdRule(AnyRsi, bullishBelow: 30m, bearishAbove: 70m);

        var mild = rule.Evaluate(new RuleContext(bars, result, 0));
        var strong = rule.Evaluate(new RuleContext(bars, result, 1));

        strong.Strength.ShouldBeGreaterThan(mild.Strength);
    }

    [Fact]
    public void should_stay_silent_while_the_indicator_has_not_warmed_up()
    {
        var bars = TestBars.FromCloses(10m, 10m, 10m);
        var result = IndicatorResult.Single(AnyRsi, [0m, 0m, 25m], firstValid: 2);

        var rule = new ThresholdRule(AnyRsi, bullishBelow: 30m, bearishAbove: 70m);

        rule.Evaluate(new RuleContext(bars, result, 1)).IsNeutral.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 2)).IsBullish.ShouldBeTrue();
    }

    [Fact]
    public void should_refuse_thresholds_that_overlap()
    {
        Should.Throw<ArgumentException>(() => new ThresholdRule(AnyRsi, bullishBelow: 70m, bearishAbove: 30m));
    }

    [Fact]
    public void should_buy_below_the_lower_band_when_betting_on_a_return_to_the_mean()
    {
        var descriptor = IndicatorDescriptor.Of("Bollinger", ("period", 20), ("multiplier", 2));
        var bars = TestBars.FromCloses(8m, 12m);
        var result = IndicatorResult.Create(descriptor, bars.Count,
        [
            (IndicatorLines.Upper, [11m, 11m], 0),
            (IndicatorLines.Middle, [10m, 10m], 0),
            (IndicatorLines.Lower, [9m, 9m], 0),
        ]);

        var meanReverting = new BandBreakoutRule(descriptor, meanReverting: true);
        var trendFollowing = new BandBreakoutRule(descriptor, meanReverting: false);

        meanReverting.Evaluate(new RuleContext(bars, result, 0)).IsBullish.ShouldBeTrue();
        meanReverting.Evaluate(new RuleContext(bars, result, 1)).IsBearish.ShouldBeTrue();

        // Le même franchissement, lu comme un suivi de tendance, dit exactement l'inverse.
        trendFollowing.Evaluate(new RuleContext(bars, result, 0)).IsBearish.ShouldBeTrue();
        trendFollowing.Evaluate(new RuleContext(bars, result, 1)).IsBullish.ShouldBeTrue();
    }

    [Fact]
    public void should_read_a_price_above_its_trailing_stop_as_an_uptrend()
    {
        var descriptor = IndicatorDescriptor.Of("Sar", ("step", 0.02m), ("maxStep", 0.2m));
        var bars = TestBars.FromCloses(12m, 8m);
        var result = IndicatorResult.Single(descriptor, [10m, 10m], 0);

        var rule = new PriceVsLevelRule(descriptor, bullishWhenAbove: true);

        rule.Evaluate(new RuleContext(bars, result, 0)).IsBullish.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 1)).IsBearish.ShouldBeTrue();
    }

    [Fact]
    public void should_read_a_price_below_its_average_traded_price_as_a_bargain()
    {
        var descriptor = IndicatorDescriptor.Of("Vwap", ("period", 20));
        var bars = TestBars.FromCloses(8m, 12m);
        var result = IndicatorResult.Single(descriptor, [10m, 10m], 0);

        var rule = new PriceVsLevelRule(descriptor, bullishWhenAbove: false);

        rule.Evaluate(new RuleContext(bars, result, 0)).IsBullish.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 1)).IsBearish.ShouldBeTrue();
    }

    [Fact]
    public void should_read_the_sign_of_a_momentum_around_its_pivot()
    {
        var descriptor = IndicatorDescriptor.Of("Momentum", ("period", 10));
        var bars = TestBars.FromCloses(10m, 10m, 10m);
        var result = IndicatorResult.Single(descriptor, [2m, 0m, -2m], 0);

        var rule = new SignRule(descriptor);

        rule.Evaluate(new RuleContext(bars, result, 0)).IsBullish.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 1)).IsNeutral.ShouldBeTrue();
        rule.Evaluate(new RuleContext(bars, result, 2)).IsBearish.ShouldBeTrue();
    }

    [Fact]
    public void should_require_the_price_and_the_cloud_to_agree_on_ichimoku()
    {
        var descriptor = IndicatorDescriptor.Of("Ichimoku", ("tenkan", 9), ("kijun", 26), ("senkouB", 52), ("displacement", 26));
        var bars = TestBars.FromCloses(12m, 12m, 8m);
        var result = IndicatorResult.Create(descriptor, bars.Count,
        [
            (IndicatorLines.TenkanSen, [10m, 10m, 10m], 0),
            (IndicatorLines.KijunSen, [10m, 10m, 10m], 0),
            (IndicatorLines.SenkouSpanA, [11m, 9m, 9m], 0),
            (IndicatorLines.SenkouSpanB, [10m, 10m, 10m], 0),
        ]);

        var rule = new IchimokuCloudRule(descriptor);

        rule.Evaluate(new RuleContext(bars, result, 0)).IsBullish.ShouldBeTrue();

        // Prix au-dessus de la Kijun mais nuage baissier : les deux ne s'accordent pas.
        rule.Evaluate(new RuleContext(bars, result, 1)).IsNeutral.ShouldBeTrue();

        rule.Evaluate(new RuleContext(bars, result, 2)).IsBearish.ShouldBeTrue();
    }

    [Fact]
    public void should_never_produce_a_signal_that_is_both_bullish_and_bearish()
    {
        // L'exclusivité n'est plus une règle à tenir mais une propriété du type.
        foreach (var direction in Enum.GetValues<SignalDirection>())
        {
            var signal = Signal.Of(direction);
            (signal.IsBullish && signal.IsBearish).ShouldBeFalse();
        }
    }

    [Fact]
    public void should_refuse_a_conviction_outside_zero_and_one()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Signal.Bullish(1.5m));
        Should.Throw<ArgumentOutOfRangeException>(() => Signal.Bearish(-0.1m));
    }
}
