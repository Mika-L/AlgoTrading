using AlgoTrading.Domain.Indicators;

namespace AlgoTrading.Domain.Strategies.Rules;

/// <summary>
/// Position de la clôture face à un niveau produit par l'indicateur — stop parabolique,
/// prix moyen pondéré.
/// <para><see cref="BullishWhenAbove"/> distingue les deux lectures opposées d'un même
/// dispositif : au-dessus du SAR la tendance est haussière (suivi), sous le VWAP le titre
/// est bon marché (retour à la moyenne).</para>
/// </summary>
public sealed class PriceVsLevelRule : ISignalRule
{
    public const string Type = "PriceVsLevel";

    public PriceVsLevelRule(IndicatorDescriptor indicator, bool bullishWhenAbove = true, string line = IndicatorLines.Value)
    {
        ArgumentNullException.ThrowIfNull(indicator);
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        Indicator = indicator;
        BullishWhenAbove = bullishWhenAbove;
        Line = line;
        WarmupBars = IndicatorCatalog.Create(indicator).WarmupBars;
    }

    public bool BullishWhenAbove { get; }

    public string Line { get; }

    public IndicatorDescriptor Indicator { get; }

    public SignalKind Kind => SignalKind.State;

    public int WarmupBars { get; }

    public string Name => BullishWhenAbove
        ? $"clôture au-dessus de {Indicator.Id}"
        : $"clôture en dessous de {Indicator.Id}";

    public Signal Evaluate(in RuleContext context)
    {
        if (!context.TryGetValue(Line, out var level))
        {
            return Signal.None;
        }

        var close = context.Close;

        if (close == level)
        {
            return Signal.None;
        }

        var above = close > level;
        return above == BullishWhenAbove ? Signal.Bullish() : Signal.Bearish();
    }
}
