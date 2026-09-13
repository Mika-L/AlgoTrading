using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Strategies;

public class SignalAggregatorTests
{
    [Fact]
    public void should_produce_no_signal_when_two_rules_say_buy_and_two_say_sell()
    {
        // L'égalité 2-2 : le code d'origine la déclarait haussière ET baissière,
        // les deux booléens étant indépendants.
        var aggregator = Build(AggregationMode.Majority, votes: 4);

        var signal = aggregator.Combine(Votes(aggregator, SignalDirection.Bullish, SignalDirection.Bullish, SignalDirection.Bearish, SignalDirection.Bearish));

        signal.IsNeutral.ShouldBeTrue();
    }

    [Fact]
    public void should_refuse_two_votes_out_of_four_as_a_majority()
    {
        // `>= Math.Ceiling(4 × 0,5)` valait `>= 2` : deux voix sur quatre suffisaient.
        var aggregator = Build(AggregationMode.Majority, votes: 4);

        var signal = aggregator.Combine(Votes(aggregator, SignalDirection.Bullish, SignalDirection.Bullish, SignalDirection.Neutral, SignalDirection.Neutral));

        signal.IsNeutral.ShouldBeTrue();
    }

    [Fact]
    public void should_accept_three_votes_out_of_four_as_a_majority()
    {
        var aggregator = Build(AggregationMode.Majority, votes: 4);

        var signal = aggregator.Combine(Votes(aggregator, SignalDirection.Bullish, SignalDirection.Bullish, SignalDirection.Bullish, SignalDirection.Neutral));

        signal.IsBullish.ShouldBeTrue();
        signal.Strength.ShouldBe(0.75m);
    }

    [Fact]
    public void should_count_a_silent_rule_as_support_withheld_not_as_an_absent_voter()
    {
        // Deux voix haussières sur trois règles dont une muette : 2 > 3 × 0,5 est vrai.
        // Mais deux voix sur quatre règles dont deux muettes ne l'est pas.
        var three = Build(AggregationMode.Majority, votes: 3);
        var four = Build(AggregationMode.Majority, votes: 4);

        three.Combine(Votes(three, SignalDirection.Bullish, SignalDirection.Bullish, SignalDirection.Neutral)).IsBullish.ShouldBeTrue();
        four.Combine(Votes(four, SignalDirection.Bullish, SignalDirection.Bullish, SignalDirection.Neutral, SignalDirection.Neutral)).IsNeutral.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0.5, false)]
    [InlineData(0.3, true)]
    public void should_honour_the_configured_threshold_in_weighted_mode(double threshold, bool expectedBullish)
    {
        // Le seuil pondéré était un 0,5 codé en dur : `MajorityThreshold` n'avait aucun effet.
        var aggregator = Build(AggregationMode.Weighted, votes: 3, threshold: (decimal)threshold);

        var signal = aggregator.Combine(Votes(aggregator, SignalDirection.Bullish, SignalDirection.Neutral, SignalDirection.Neutral));

        signal.IsBullish.ShouldBe(expectedBullish);
    }

    [Fact]
    public void should_let_a_heavier_rule_carry_the_vote_in_weighted_mode()
    {
        var aggregator = Build(AggregationMode.Weighted, votes: 3, threshold: 0.5m, weights: [3m, 1m, 1m]);

        var signal = aggregator.Combine(Votes(aggregator, SignalDirection.Bullish, SignalDirection.Bearish, SignalDirection.Bearish));

        signal.IsBullish.ShouldBeTrue();
        signal.Strength.ShouldBe(0.6m);
    }

    [Fact]
    public void should_require_every_rule_to_agree_in_consensus_mode()
    {
        var aggregator = Build(AggregationMode.Consensus, votes: 3);

        aggregator.Combine(Votes(aggregator, SignalDirection.Bullish, SignalDirection.Bullish, SignalDirection.Bullish)).IsBullish.ShouldBeTrue();
        aggregator.Combine(Votes(aggregator, SignalDirection.Bullish, SignalDirection.Bullish, SignalDirection.Neutral)).IsNeutral.ShouldBeTrue();
    }

    [Fact]
    public void should_treat_a_single_contradiction_as_a_conflict_in_any_mode()
    {
        var aggregator = Build(AggregationMode.Any, votes: 3);

        aggregator.Combine(Votes(aggregator, SignalDirection.Bullish, SignalDirection.Neutral, SignalDirection.Neutral)).IsBullish.ShouldBeTrue();
        aggregator.Combine(Votes(aggregator, SignalDirection.Bullish, SignalDirection.Bearish, SignalDirection.Neutral)).IsNeutral.ShouldBeTrue();
    }

    [Fact]
    public void should_refuse_a_policy_without_any_rule()
    {
        Should.Throw<ArgumentException>(() => new SignalAggregator([], AggregationMode.Majority));
    }

    [Fact]
    public void should_refuse_a_rule_whose_weight_is_not_positive()
    {
        var rule = new StubRule(SignalDirection.Neutral);

        Should.Throw<ArgumentException>(() => new SignalAggregator([(rule, 0m)], AggregationMode.Weighted));
    }

    private static SignalAggregator Build(AggregationMode mode, int votes, decimal threshold = 0.5m, decimal[]? weights = null)
    {
        var rules = Enumerable.Range(0, votes)
            .Select(i => ((ISignalRule)new StubRule(SignalDirection.Neutral, weights?[i] ?? 1m), weights?[i] ?? 1m))
            .ToArray();

        return new SignalAggregator(rules, mode, threshold);
    }

    /// <summary>Reconstitue les votes en réutilisant les poids déclarés à la construction.</summary>
    private static RuleVote[] Votes(SignalAggregator aggregator, params SignalDirection[] directions)
    {
        var rules = aggregator.Rules;
        directions.Length.ShouldBe(rules.Count);

        return [.. directions.Select((direction, i) => new RuleVote(rules[i], ((StubRule)rules[i]).Weight, Signal.Of(direction)))];
    }

    private sealed class StubRule(SignalDirection direction, decimal weight = 1m) : ISignalRule
    {
        private static int _counter;
        private readonly int _id = Interlocked.Increment(ref _counter);

        public decimal Weight { get; } = weight;

        public string Name => $"règle {_id}";

        public IndicatorDescriptor Indicator { get; } = IndicatorDescriptor.Of("Rsi", ("period", 14));

        public SignalKind Kind => SignalKind.State;

        public int WarmupBars => 0;

        public Signal Evaluate(in RuleContext context) => Signal.Of(direction);
    }
}
