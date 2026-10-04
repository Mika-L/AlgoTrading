using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Strategies.Rules;
using AlgoTrading.Web.Forms;
using Shouldly;

namespace AlgoTrading.Web.Tests.Forms;

/// <summary>
/// L'éditeur ne doit rien changer à ce qu'il n'a pas touché : une stratégie ouverte puis
/// enregistrée telle quelle garde son empreinte, et ses runs restent reconnus comme siens.
/// </summary>
public class StrategyFormTests
{
    private static readonly IndicatorLineCatalog Catalog = IndicatorLineCatalog.Discover();

    private const string Complete = """
        {
          "name": "Complète",
          "entry": {
            "mode": "Weighted",
            "threshold": 0.6,
            "rules": [
              { "type": "BandBreakout", "indicator": "Bollinger", "parameters": { "period": 20, "multiplier": 2 }, "meanReverting": false, "weight": 2 },
              { "type": "Threshold", "indicator": "Rsi", "parameters": { "period": 14 }, "bullishBelow": 30, "bearishAbove": 70, "asEvent": true },
              { "type": "Crossover", "indicator": "Macd", "fastLine": "value", "slowLine": "signal", "mode": "Relative" }
            ]
          },
          "exit": {
            "mode": "Any",
            "rules": [ { "type": "Sign", "indicator": "Momentum", "pivot": 0 } ]
          },
          "sizing": { "mode": "FixedNotional", "value": 5000 },
          "risk": { "stopLoss": 0.05, "takeProfit": 0.15, "trailingRate": 0.1, "trailingAtr": { "multiple": 3, "period": 14 } },
          "execution": { "commissionRate": 0.0010, "minimumCommission": 1, "slippageRate": 0.0005, "orderValidityDays": 2, "exitMode": "Fraction", "exitFraction": 0.5 }
        }
        """;

    [Fact]
    public void should_keep_the_fingerprint_of_a_strategy_opened_and_saved_unchanged()
    {
        var original = StrategyDefinition.FromJson(Complete);

        var roundTrip = StrategyForm.From(original).ToDefinition();

        roundTrip.Fingerprint.ShouldBe(original.Fingerprint);
    }

    [Fact]
    public void should_keep_the_fingerprint_of_a_strategy_relying_on_default_costs()
    {
        // Les frais par défaut s'écrivent 0,0010 : rendus en 0,001, ils changeraient l'empreinte.
        var original = StrategyDefinition.FromJson("""
            { "name": "Minimale", "entry": { "rules": [ { "type": "Threshold", "indicator": "Rsi", "bullishBelow": 30, "bearishAbove": 70 } ] } }
            """);

        StrategyForm.From(original).ToDefinition().Fingerprint.ShouldBe(original.Fingerprint);
    }

    [Fact]
    public void should_present_rates_as_percentages_and_store_them_as_fractions()
    {
        var form = StrategyForm.From(StrategyDefinition.FromJson(Complete));

        form.StopLossPercent.ShouldBe(5m);
        form.CommissionPercent.ShouldBe(0.1m);

        form.StopLossPercent = 8m;
        form.TrailingRatePercent = null;

        var strategy = form.ToDefinition();
        strategy.Risk.StopLoss.ShouldBe(0.08m);
        strategy.Risk.TrailingRate.ShouldBeNull();
    }

    [Fact]
    public void should_leave_default_lines_out_of_the_file()
    {
        var rule = RuleForm.For(Catalog.Find("Rsi")!).ToConfig();

        rule.Line.ShouldBeNull();
        rule.BullishBelow.ShouldBe(30m);
    }

    [Fact]
    public void should_switch_to_lines_the_new_indicator_actually_produces()
    {
        var rule = RuleForm.For(Catalog.Find("Ema")!);

        rule.ChangeIndicator(Catalog.Find("Macd")!);

        rule.FastLine.ShouldBe(IndicatorLines.Value);
        rule.SlowLine.ShouldBe(IndicatorLines.Signal);
        rule.Problems(Catalog).ShouldBeEmpty();
    }

    [Fact]
    public void should_adopt_the_default_rule_of_the_new_indicator()
    {
        var rule = RuleForm.For(Catalog.Find("Rsi")!);
        rule.Weight = 2m;
        rule.AsEvent = true;

        rule.ChangeIndicator(Catalog.Find("Bollinger")!);

        rule.Type.ShouldBe(BandBreakoutRule.Type);
        rule.Weight.ShouldBe(2m);
        rule.AsEvent.ShouldBeTrue();
        rule.Problems(Catalog).ShouldBeEmpty();
    }

    [Fact]
    public void should_take_the_thresholds_of_the_new_indicator_rather_than_keep_the_old_ones()
    {
        var rule = RuleForm.For(Catalog.Find("Rsi")!);

        rule.ChangeIndicator(Catalog.Find("Cci")!);

        rule.Type.ShouldBe(ThresholdRule.Type);
        rule.BullishBelow.ShouldBe(-100m);
        rule.BearishAbove.ShouldBe(100m);
    }

    [Fact]
    public void should_never_replace_the_indicator_when_the_type_changes()
    {
        var rule = RuleForm.For(Catalog.Find("Rsi")!);

        rule.ChangeType(SignRule.Type, Catalog);
        rule.ChangeType(ThresholdRule.Type, Catalog);

        rule.Indicator.ShouldBe("Rsi");
        rule.Problems(Catalog).ShouldBeEmpty();
    }

    [Fact]
    public void should_keep_the_indicator_but_fix_its_line_when_the_type_changes()
    {
        var rule = RuleForm.For(Catalog.Find("Ema")!);

        rule.ChangeType(ThresholdRule.Type, Catalog);

        rule.Indicator.ShouldBe("Ema");
        rule.Problems(Catalog).ShouldBeEmpty();
    }

    [Fact]
    public void should_flag_a_rule_reading_a_line_its_indicator_does_not_produce()
    {
        // La validation du domaine laisse passer cette règle : elle ne voterait simplement jamais.
        var form = new StrategyForm
        {
            Name = "Muette",
            Entry = new SignalPolicyForm
            {
                Rules = [new RuleForm { Type = ThresholdRule.Type, Indicator = "Ema", Line = IndicatorLines.Value }],
            },
        };

        Should.NotThrow(() => form.ToDefinition().Validate());
        form.Problems(Catalog).ShouldHaveSingleItem().ShouldContain("« value »");
    }
}
