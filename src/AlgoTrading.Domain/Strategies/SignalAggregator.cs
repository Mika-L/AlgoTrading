using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Strategies;

public enum AggregationMode
{
    /// <summary>Toutes les règles doivent dire la même chose.</summary>
    Consensus,

    /// <summary>Une part des voix, chaque règle comptant pour une.</summary>
    Majority,

    /// <summary>Au moins une voix, et aucune voix contraire.</summary>
    Any,

    /// <summary>Une part des voix, chaque règle comptant pour son poids.</summary>
    Weighted,
}

/// <summary>Contribution d'une règle à un signal agrégé, pour pouvoir l'expliquer.</summary>
public readonly record struct RuleVote(ISignalRule Rule, decimal Weight, Signal Signal);

/// <summary>
/// Combine les verdicts des règles. Quatre corrections par rapport à l'agrégateur d'origine :
/// <list type="bullet">
/// <item>haussier et baissier sont comptés séparément, et une égalité donne un signal neutre
/// au lieu d'un signal « haussier et baissier à la fois » ;</item>
/// <item>la majorité est <b>stricte</b> : <c>bull &gt; total × seuil</c>. Le code d'origine
/// testait <c>&gt;= Math.Ceiling(4 × 0,5)</c>, soit 2 voix sur 4 — ce n'est pas une majorité ;</item>
/// <item>le mode pondéré utilise le seuil configuré, au lieu d'un <c>0,5</c> codé en dur qui
/// rendait <c>MajorityThreshold</c> sans effet ;</item>
/// <item>les poids sont portés par la configuration de chaque règle, et non par un dictionnaire
/// à clés par référence dont seul le <c>Count</c> était validé.</item>
/// </list>
/// </summary>
public sealed class SignalAggregator
{
    private readonly (ISignalRule Rule, decimal Weight)[] _rules;

    public SignalAggregator(IReadOnlyList<(ISignalRule Rule, decimal Weight)> rules, AggregationMode mode, decimal threshold = 0.5m)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Count == 0)
        {
            throw new ArgumentException("Une politique de signal doit comporter au moins une règle.", nameof(rules));
        }

        if (threshold is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Le seuil est une fraction entre 0 et 1.");
        }

        foreach (var (rule, weight) in rules)
        {
            ArgumentNullException.ThrowIfNull(rule);

            if (weight <= 0m)
            {
                throw new ArgumentException($"Le poids de la règle « {rule.Name} » doit être strictement positif.", nameof(rules));
            }
        }

        _rules = [.. rules];
        Mode = mode;
        Threshold = threshold;
        WarmupBars = rules.Max(static r => r.Rule.WarmupBars);
        Indicators = [.. rules.Select(static r => r.Rule.Indicator).DistinctBy(static d => d.Id).OrderBy(static d => d.Id, StringComparer.Ordinal)];
    }

    public AggregationMode Mode { get; }

    public decimal Threshold { get; }

    public int WarmupBars { get; }

    /// <summary>Les indicateurs distincts dont dépendent les règles — de quoi n'en calculer aucun deux fois.</summary>
    public IReadOnlyList<IndicatorDescriptor> Indicators { get; }

    public IReadOnlyList<ISignalRule> Rules => [.. _rules.Select(static r => r.Rule)];

    public Signal Evaluate(BarSeries bars, IReadOnlyDictionary<string, IndicatorResult> results, int barIndex) =>
        Combine(Poll(bars, results, barIndex));

    /// <summary>Verdict de chaque règle, dans l'ordre de déclaration, pour expliquer un signal.</summary>
    public RuleVote[] Poll(BarSeries bars, IReadOnlyDictionary<string, IndicatorResult> results, int barIndex)
    {
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentNullException.ThrowIfNull(results);

        var votes = new RuleVote[_rules.Length];

        for (var i = 0; i < _rules.Length; i++)
        {
            var (rule, weight) = _rules[i];

            if (!results.TryGetValue(rule.Indicator.Id, out var result))
            {
                throw new InvalidOperationException($"Le résultat de {rule.Indicator.Id}, requis par la règle « {rule.Name} », n'a pas été calculé.");
            }

            var signal = rule.Evaluate(new RuleContext(bars, result, barIndex));
            votes[i] = new RuleVote(rule, weight, signal);
        }

        return votes;
    }

    public Signal Combine(IReadOnlyList<RuleVote> votes)
    {
        ArgumentNullException.ThrowIfNull(votes);

        var bull = 0m;
        var bear = 0m;
        var total = 0m;

        foreach (var vote in votes)
        {
            // En mode Majorité chaque règle pèse une voix ; ailleurs elle pèse son poids.
            var weight = Mode == AggregationMode.Majority ? 1m : vote.Weight;

            // Les neutres ne comptent dans aucun numérateur, mais restent au dénominateur :
            // une règle muette est une règle qui ne soutient pas.
            total += weight;

            switch (vote.Signal.Direction)
            {
                case SignalDirection.Bullish:
                    bull += weight;
                    break;
                case SignalDirection.Bearish:
                    bear += weight;
                    break;
                case SignalDirection.Neutral:
                default:
                    break;
            }
        }

        if (total <= 0m)
        {
            return Signal.None;
        }

        return Mode switch
        {
            AggregationMode.Consensus => Decide(bull == total, bear == total, bull, bear, total),
            AggregationMode.Any => Decide(bull > 0m && bear == 0m, bear > 0m && bull == 0m, bull, bear, total),
            AggregationMode.Majority or AggregationMode.Weighted => Decide(bull > total * Threshold, bear > total * Threshold, bull, bear, total),
            _ => Signal.None,
        };
    }

    /// <summary>
    /// Tranche entre les deux camps. Si les deux qualifient, la direction nette l'emporte ;
    /// à égalité, aucun signal — deux haussiers contre deux baissiers ne disent rien.
    /// </summary>
    private static Signal Decide(bool bullQualifies, bool bearQualifies, decimal bull, decimal bear, decimal total)
    {
        if (bullQualifies && bearQualifies)
        {
            if (bull == bear)
            {
                return Signal.None;
            }

            return bull > bear ? Signal.Bullish(bull / total) : Signal.Bearish(bear / total);
        }

        if (bullQualifies)
        {
            return Signal.Bullish(bull / total);
        }

        return bearQualifies ? Signal.Bearish(bear / total) : Signal.None;
    }
}
