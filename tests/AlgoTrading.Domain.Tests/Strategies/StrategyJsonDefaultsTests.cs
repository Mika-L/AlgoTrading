using AlgoTrading.Domain.Strategies;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Strategies;

/// <summary>
/// Non-régression d'un défaut silencieux et coûteux : le générateur de sérialisation
/// n'exécute pas les initialiseurs de propriétés. Un réglage absent d'un fichier de stratégie
/// retombait donc à zéro — frais nuls, seuil de vote nul, taille de position nulle — sans le
/// moindre message. Les valeurs par défaut sont désormais portées par des constructeurs.
/// </summary>
public class StrategyJsonDefaultsTests
{
    private const string Minimal = """
        {
          "name": "Minimale",
          "entry": {
            "rules": [
              { "type": "Threshold", "indicator": "Rsi", "bullishBelow": 30, "bearishAbove": 70 }
            ]
          }
        }
        """;

    [Fact]
    public void should_charge_the_usual_costs_to_a_strategy_that_says_nothing_about_them()
    {
        var strategy = StrategyDefinition.FromJson(Minimal);

        strategy.Execution.CommissionRate.ShouldBe(ExecutionPolicy.DefaultCommissionRate);
        strategy.Execution.MinimumCommission.ShouldBe(ExecutionPolicy.DefaultMinimumCommission);
        strategy.Execution.SlippageRate.ShouldBe(ExecutionPolicy.DefaultSlippageRate);
        strategy.Execution.OrderValidityDays.ShouldBe(ExecutionPolicy.DefaultOrderValidityDays);
    }

    [Fact]
    public void should_require_a_real_majority_when_the_vote_threshold_is_left_out()
    {
        var strategy = StrategyDefinition.FromJson(Minimal);

        strategy.Entry.Mode.ShouldBe(SignalPolicy.DefaultMode);
        strategy.Entry.Threshold.ShouldBe(SignalPolicy.DefaultThreshold);
    }

    [Fact]
    public void should_size_positions_on_the_usual_fraction_when_none_is_given()
    {
        var strategy = StrategyDefinition.FromJson(Minimal);

        strategy.Sizing.Mode.ShouldBe(PositionSizing.DefaultMode);
        strategy.Sizing.Value.ShouldBe(PositionSizing.DefaultValue);
    }

    [Fact]
    public void should_give_every_rule_an_equal_say_when_no_weight_is_given()
    {
        var strategy = StrategyDefinition.FromJson(Minimal);

        strategy.Entry.Rules[0].Weight.ShouldBe(RuleConfig.DefaultWeight);
        strategy.Entry.Rules[0].Parameters.ShouldNotBeNull();
    }

    [Fact]
    public void should_leave_the_position_unprotected_when_no_risk_policy_is_given()
    {
        // Décision arbitrée : la plomberie du risque est livrée mais inactive par défaut.
        StrategyDefinition.FromJson(Minimal).Risk.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void should_give_a_trailing_stop_the_usual_atr_period_when_it_omits_one()
    {
        var suiveur = """
            {
              "name": "Suiveur",
              "entry": {
                "rules": [
                  { "type": "Sign", "indicator": "Momentum" }
                ]
              },
              "risk": { "trailingAtr": { "multiple": 3 } }
            }
            """;

        var strategy = StrategyDefinition.FromJson(suiveur);

        strategy.Risk.TrailingAtr.ShouldNotBeNull();
        strategy.Risk.TrailingAtr!.Multiple.ShouldBe(3m);
        strategy.Risk.TrailingAtr.Period.ShouldBe(TrailingAtrStop.DefaultPeriod);
        strategy.Risk.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void should_read_a_trailing_fraction_as_a_fraction()
    {
        // Comme tout taux du projet : 0,1 vaut dix pour cent, jamais un dixième de pour cent.
        var suiveur = """
            {
              "name": "Suiveur",
              "entry": {
                "rules": [
                  { "type": "Sign", "indicator": "Momentum" }
                ]
              },
              "risk": { "trailingRate": 0.1 }
            }
            """;

        var strategy = StrategyDefinition.FromJson(suiveur);

        strategy.Risk.TrailingRate.ShouldBe(0.1m);
        strategy.Risk.TrailingAtr.ShouldBeNull();
        strategy.Risk.HasTrailingStop.ShouldBeTrue();
    }

    [Fact]
    public void should_write_a_trailing_stop_without_the_calculation_it_implies()
    {
        // Le descripteur d'ATR se déduit de la période : un fichier de stratégie porte une
        // intention, pas le plan de calcul qui en découle.
        var strategy = StrategyDefinition.FromJson(Minimal) with
        {
            Risk = new RiskPolicy { TrailingAtr = new TrailingAtrStop { Multiple = 3m } },
        };

        var json = strategy.ToJson();

        json.ShouldContain("trailingAtr");
        json.ShouldNotContain("Atr(period");
    }

    [Fact]
    public void should_still_honour_a_setting_that_is_written_down()
    {
        var explicitCosts = """
            {
              "name": "Sans frais",
              "entry": {
                "mode": "Consensus",
                "threshold": 0.9,
                "rules": [
                  { "type": "Sign", "indicator": "Momentum", "weight": 3 }
                ]
              },
              "sizing": { "mode": "FixedNotional", "value": 5000 },
              "execution": { "commissionRate": 0, "minimumCommission": 0, "slippageRate": 0 }
            }
            """;

        var strategy = StrategyDefinition.FromJson(explicitCosts);

        strategy.Entry.Mode.ShouldBe(AggregationMode.Consensus);
        strategy.Entry.Threshold.ShouldBe(0.9m);
        strategy.Entry.Rules[0].Weight.ShouldBe(3m);
        strategy.Sizing.Mode.ShouldBe(SizingMode.FixedNotional);
        strategy.Sizing.Value.ShouldBe(5_000m);
        strategy.Execution.CommissionRate.ShouldBe(0m);
    }

    [Fact]
    public void should_keep_every_setting_through_a_full_round_trip()
    {
        var original = StrategyDefinition.FromJson(Minimal);

        var restored = StrategyDefinition.FromJson(original.ToJson());

        restored.Fingerprint.ShouldBe(original.Fingerprint);
        restored.Execution.ShouldBe(original.Execution);
        restored.Sizing.ShouldBe(original.Sizing);
        restored.Entry.Threshold.ShouldBe(original.Entry.Threshold);
    }
}
