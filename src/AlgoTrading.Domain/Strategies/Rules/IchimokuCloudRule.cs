using AlgoTrading.Domain.Indicators;

namespace AlgoTrading.Domain.Strategies.Rules;

/// <summary>
/// Lecture du nuage Ichimoku : la clôture doit être du bon côté de la Kijun <b>et</b> le
/// nuage orienté dans le même sens.
/// </summary>
public sealed class IchimokuCloudRule : ISignalRule
{
    public const string Type = "IchimokuCloud";

    public IchimokuCloudRule(IndicatorDescriptor indicator)
    {
        ArgumentNullException.ThrowIfNull(indicator);

        Indicator = indicator;
        WarmupBars = IndicatorCatalog.Create(indicator).WarmupBars;
    }

    public IndicatorDescriptor Indicator { get; }

    public SignalKind Kind => SignalKind.State;

    public int WarmupBars { get; }

    public string Name => $"{Indicator.Id} : clôture et nuage alignés";

    public Signal Evaluate(in RuleContext context)
    {
        if (!context.TryGetValue(IndicatorLines.KijunSen, out var kijun) ||
            !context.TryGetValue(IndicatorLines.SenkouSpanA, out var spanA) ||
            !context.TryGetValue(IndicatorLines.SenkouSpanB, out var spanB))
        {
            return Signal.None;
        }

        var close = context.Close;

        if (close > kijun && spanA > spanB)
        {
            return Signal.Bullish();
        }

        if (close < kijun && spanA < spanB)
        {
            return Signal.Bearish();
        }

        return Signal.None;
    }
}
