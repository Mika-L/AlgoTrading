using AlgoTrading.Domain.Indicators;

namespace AlgoTrading.Domain.Strategies.Rules;

/// <summary>Manière de lire la position relative de deux lignes.</summary>
public enum CrossoverMode
{
    /// <summary>Ne se prononce qu'à la barre du croisement — un événement.</summary>
    Cross,

    /// <summary>Se prononce tant qu'une ligne domine l'autre — un état.</summary>
    Relative,
}

/// <summary>
/// Position relative de deux lignes d'un même indicateur : MME courte contre longue,
/// ligne MACD contre sa ligne de signal.
/// <para>Le « hier » du code d'origine cherchait <c>date.AddDays(-1)</c>. Un lundi, la veille
/// est un dimanche : aucune valeur, donc aucun croisement détecté. Ici la barre précédente
/// est <c>BarIndex - 1</c> — le vendredi, quoi qu'en dise le calendrier.</para>
/// </summary>
public sealed class CrossoverRule : ISignalRule
{
    public const string Type = "Crossover";

    public CrossoverRule(
        IndicatorDescriptor indicator,
        string fastLine = IndicatorLines.Fast,
        string slowLine = IndicatorLines.Slow,
        CrossoverMode mode = CrossoverMode.Cross)
    {
        ArgumentNullException.ThrowIfNull(indicator);
        ArgumentException.ThrowIfNullOrWhiteSpace(fastLine);
        ArgumentException.ThrowIfNullOrWhiteSpace(slowLine);

        if (string.Equals(fastLine, slowLine, StringComparison.Ordinal))
        {
            throw new ArgumentException("Croiser une ligne avec elle-même n'a pas de sens.", nameof(slowLine));
        }

        Indicator = indicator;
        FastLine = fastLine;
        SlowLine = slowLine;
        Mode = mode;

        var warmup = IndicatorCatalog.Create(indicator).WarmupBars;

        // Un croisement se lit sur deux barres : il lui en faut une de plus.
        WarmupBars = mode == CrossoverMode.Cross ? warmup + 1 : warmup;
    }

    public string FastLine { get; }

    public string SlowLine { get; }

    public CrossoverMode Mode { get; }

    public IndicatorDescriptor Indicator { get; }

    public SignalKind Kind => Mode == CrossoverMode.Cross ? SignalKind.Event : SignalKind.State;

    public int WarmupBars { get; }

    public string Name => Mode == CrossoverMode.Cross
        ? $"{Indicator.Id} : croisement {FastLine} / {SlowLine}"
        : $"{Indicator.Id} : {FastLine} au-dessus de {SlowLine}";

    public Signal Evaluate(in RuleContext context)
    {
        if (!context.TryGetValue(FastLine, out var fast) || !context.TryGetValue(SlowLine, out var slow))
        {
            return Signal.None;
        }

        if (Mode == CrossoverMode.Relative)
        {
            return Compare(fast, slow);
        }

        if (!context.TryGetPrevious(FastLine, out var previousFast) || !context.TryGetPrevious(SlowLine, out var previousSlow))
        {
            return Signal.None;
        }

        var wasBelow = previousFast < previousSlow;
        var wasAbove = previousFast > previousSlow;

        if (wasBelow && fast > slow)
        {
            return Signal.Bullish();
        }

        if (wasAbove && fast < slow)
        {
            return Signal.Bearish();
        }

        return Signal.None;
    }

    private static Signal Compare(decimal fast, decimal slow)
    {
        if (fast > slow)
        {
            return Signal.Bullish();
        }

        return fast < slow ? Signal.Bearish() : Signal.None;
    }
}
