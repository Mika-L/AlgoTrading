using AlgoTrading.Domain.Indicators;

namespace AlgoTrading.Domain.Strategies.Rules;

/// <summary>Signe d'un indicateur centré sur zéro — le momentum, l'histogramme MACD.</summary>
public sealed class SignRule : ISignalRule
{
    public const string Type = "Sign";

    public SignRule(IndicatorDescriptor indicator, decimal pivot = 0m, string line = IndicatorLines.Value)
    {
        ArgumentNullException.ThrowIfNull(indicator);
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        Indicator = indicator;
        Pivot = pivot;
        Line = line;
        WarmupBars = IndicatorCatalog.Create(indicator).WarmupBars;
    }

    public decimal Pivot { get; }

    public string Line { get; }

    public IndicatorDescriptor Indicator { get; }

    public SignalKind Kind => SignalKind.State;

    public int WarmupBars { get; }

    public string Name => $"{Indicator.Id} de part et d'autre de {Pivot}";

    public Signal Evaluate(in RuleContext context)
    {
        if (!context.TryGetValue(Line, out var value))
        {
            return Signal.None;
        }

        if (value > Pivot)
        {
            return Signal.Bullish();
        }

        return value < Pivot ? Signal.Bearish() : Signal.None;
    }
}
