using AlgoTrading.Domain.Indicators;

namespace AlgoTrading.Domain.Strategies.Rules;

/// <summary>
/// Convertit une règle d'état en règle d'événement : le signal n'est émis qu'à la barre où
/// la direction change.
/// <para>C'est le remède au mélange relevé par <see cref="SignalKind"/> — faire voter dans la
/// même majorité une règle qui parle tous les jours et une qui parle trois fois par an.
/// Le passage effectif en événement divise la fréquence de trading par un ordre de grandeur :
/// il reste donc un choix explicite, jamais un défaut.</para>
/// </summary>
public sealed class EdgeRule : ISignalRule
{
    public const string Type = "Edge";

    private EdgeRule(ISignalRule inner) => Inner = inner;

    public static ISignalRule Wrap(ISignalRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        // Envelopper un événement ne changerait rien.
        return rule.Kind == SignalKind.Event ? rule : new EdgeRule(rule);
    }

    public ISignalRule Inner { get; }

    public string Name => $"{Inner.Name} (au basculement)";

    public IndicatorDescriptor Indicator => Inner.Indicator;

    public SignalKind Kind => SignalKind.Event;

    public int WarmupBars => Inner.WarmupBars + 1;

    public Signal Evaluate(in RuleContext context)
    {
        if (context.BarIndex <= 0)
        {
            return Signal.None;
        }

        var current = Inner.Evaluate(context);
        if (current.IsNeutral)
        {
            return Signal.None;
        }

        var previousContext = context with { BarIndex = context.BarIndex - 1 };
        var previous = Inner.Evaluate(previousContext);

        return previous.Direction == current.Direction ? Signal.None : current;
    }
}
