using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Strategies.Rules;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Strategies;

public class StrategyDefinitionTests
{
    private static StrategyDefinition Sample() => new()
    {
        Name = "Bollinger et RSI",
        Entry = new SignalPolicy
        {
            Mode = AggregationMode.Majority,
            Threshold = 0.5m,
            Rules =
            [
                RuleRegistry.DefaultFor("Bollinger"),
                RuleRegistry.DefaultFor("Rsi"),
                RuleRegistry.DefaultFor("Macd"),
            ],
        },
    };

    [Fact]
    public void should_survive_a_round_trip_through_its_serialized_form()
    {
        var original = Sample();

        var restored = StrategyDefinition.FromJson(original.ToJson());

        restored.Name.ShouldBe(original.Name);
        restored.Entry.Rules.Count.ShouldBe(3);
        restored.Fingerprint.ShouldBe(original.Fingerprint);
    }

    [Fact]
    public void should_keep_the_same_fingerprint_whatever_the_order_the_rules_are_declared_in()
    {
        var one = Sample();
        var other = one with
        {
            Entry = one.Entry with { Rules = [.. one.Entry.Rules.Reverse()] },
        };

        other.Fingerprint.ShouldBe(one.Fingerprint);
    }

    [Fact]
    public void should_change_its_fingerprint_when_a_threshold_changes()
    {
        var one = Sample();
        var other = one with { Entry = one.Entry with { Threshold = 0.75m } };

        other.Fingerprint.ShouldNotBe(one.Fingerprint);
    }

    [Fact]
    public void should_change_its_fingerprint_when_an_indicator_setting_changes()
    {
        var one = Sample();
        var tighterRsi = new RuleConfig
        {
            Type = ThresholdRule.Type,
            Indicator = "Rsi",
            Parameters = new Dictionary<string, decimal> { ["period"] = 7m },
            BullishBelow = 30m,
            BearishAbove = 70m,
        };

        var other = one with
        {
            Entry = one.Entry with { Rules = [one.Entry.Rules[0], tighterRsi, one.Entry.Rules[2]] },
        };

        other.Fingerprint.ShouldNotBe(one.Fingerprint);
    }

    [Fact]
    public void should_let_the_same_indicator_appear_twice_with_different_settings()
    {
        // Le filtrage par GetType() du code d'origine l'interdisait :
        // RSI(14) et RSI(7) étaient « le même indicateur ».
        var strategy = new StrategyDefinition
        {
            Name = "Deux RSI",
            Entry = new SignalPolicy
            {
                Mode = AggregationMode.Consensus,
                Rules =
                [
                    new RuleConfig { Type = ThresholdRule.Type, Indicator = "Rsi", Parameters = new() { ["period"] = 14m }, BullishBelow = 30m, BearishAbove = 70m },
                    new RuleConfig { Type = ThresholdRule.Type, Indicator = "Rsi", Parameters = new() { ["period"] = 7m }, BullishBelow = 20m, BearishAbove = 80m },
                ],
            },
        };

        var aggregator = strategy.Entry.ToAggregator();

        aggregator.Indicators.Count.ShouldBe(2);
        aggregator.Indicators.Select(static i => i.Id).ShouldBe(["Rsi(period=14)", "Rsi(period=7)"], ignoreOrder: true);
    }

    [Fact]
    public void should_compute_each_distinct_indicator_only_once_across_rules()
    {
        var strategy = new StrategyDefinition
        {
            Name = "Deux lectures du même RSI",
            Entry = new SignalPolicy
            {
                Rules =
                [
                    new RuleConfig { Type = ThresholdRule.Type, Indicator = "Rsi", BullishBelow = 30m, BearishAbove = 70m },
                    new RuleConfig { Type = ThresholdRule.Type, Indicator = "Rsi", BullishBelow = 20m, BearishAbove = 80m },
                ],
            },
        };

        strategy.Entry.ToAggregator().Indicators.Count.ShouldBe(1);
    }

    [Fact]
    public void should_come_without_any_stop_loss_until_one_is_asked_for()
    {
        // Décision arbitrée : la plomberie du risque est livrée mais inactive, pour que le
        // premier run après refactor reste comparable à l'ancien.
        Sample().Risk.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void should_charge_a_commission_and_a_slippage_by_default()
    {
        // Un backtest à frais nuls sur quarante titres avec signal quotidien n'est pas une mesure.
        var execution = Sample().Execution;

        execution.CommissionRate.ShouldBeGreaterThan(0m);
        execution.SlippageRate.ShouldBeGreaterThan(0m);
    }

    [Fact]
    public void should_size_a_position_on_the_whole_portfolio_not_on_the_leftover_cash()
    {
        Sample().Sizing.Mode.ShouldBe(SizingMode.EquityFraction);
    }

    [Fact]
    public void should_refuse_a_strategy_without_any_entry_rule()
    {
        var empty = new StrategyDefinition { Name = "Vide", Entry = new SignalPolicy() };

        Should.Throw<InvalidOperationException>(empty.Validate);
    }

    [Fact]
    public void should_refuse_a_position_larger_than_the_whole_portfolio()
    {
        var oversized = Sample() with { Sizing = new PositionSizing { Value = 1.5m } };

        Should.Throw<InvalidOperationException>(oversized.Validate);
    }

    [Fact]
    public void should_refuse_an_unknown_rule_type()
    {
        var unknown = new RuleConfig { Type = "Intuition", Indicator = "Rsi" };

        Should.Throw<ArgumentException>(() => RuleRegistry.Create(unknown));
    }

    [Fact]
    public void should_refuse_a_threshold_rule_that_omits_its_thresholds()
    {
        var incomplete = new RuleConfig { Type = ThresholdRule.Type, Indicator = "Rsi" };

        Should.Throw<ArgumentException>(() => RuleRegistry.Create(incomplete));
    }

    [Fact]
    public void should_refuse_to_give_the_volatility_gauge_a_direction_of_its_own()
    {
        // L'ATR n'a pas de règle par défaut : lui en donner une, c'était le comparer à la
        // moyenne de toute la série, futur compris.
        Should.Throw<ArgumentException>(() => RuleRegistry.DefaultFor("Atr"));
    }

    [Fact]
    public void should_offer_a_default_rule_for_every_directional_indicator()
    {
        var catalog = RuleRegistry.DefaultCatalog();

        catalog.Count.ShouldBe(12);
        Should.NotThrow(() => catalog.Select(RuleRegistry.Create).ToList());
    }
}
