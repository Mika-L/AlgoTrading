using AlgoTrading.Domain.Indicators;

namespace AlgoTrading.Domain.Strategies.Rules;

/// <summary>
/// Excès d'un oscillateur : sous <see cref="BullishBelow"/> le marché est survendu,
/// au-dessus de <see cref="BearishAbove"/> il est suracheté.
/// <para>Couvre RSI, CCI, stochastique et Williams %R, dont les seuils étaient jusqu'ici
/// des littéraux disséminés dans chaque indicateur (±100, 20/80, −80/−20).</para>
/// </summary>
public sealed class ThresholdRule : ISignalRule
{
    public const string Type = "Threshold";

    public ThresholdRule(IndicatorDescriptor indicator, decimal bullishBelow, decimal bearishAbove, string line = IndicatorLines.Value)
    {
        ArgumentNullException.ThrowIfNull(indicator);
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        if (bullishBelow >= bearishAbove)
        {
            throw new ArgumentException($"Le seuil de survente ({bullishBelow}) doit être inférieur au seuil de surachat ({bearishAbove}).", nameof(bullishBelow));
        }

        Indicator = indicator;
        BullishBelow = bullishBelow;
        BearishAbove = bearishAbove;
        Line = line;
        WarmupBars = IndicatorCatalog.Create(indicator).WarmupBars;
    }

    public decimal BullishBelow { get; }

    public decimal BearishAbove { get; }

    public string Line { get; }

    public IndicatorDescriptor Indicator { get; }

    public SignalKind Kind => SignalKind.State;

    public int WarmupBars { get; }

    public string Name => $"{Indicator.Id} hors de [{BullishBelow} ; {BearishAbove}]";

    public Signal Evaluate(in RuleContext context)
    {
        if (!context.TryGetValue(Line, out var value))
        {
            return Signal.None;
        }

        if (value < BullishBelow)
        {
            return Signal.Bullish(Conviction(BullishBelow - value));
        }

        if (value > BearishAbove)
        {
            return Signal.Bearish(Conviction(value - BearishAbove));
        }

        return Signal.None;
    }

    /// <summary>
    /// Plus l'oscillateur dépasse son seuil, plus la conviction est forte, jusqu'à saturation
    /// à une largeur de bande de dépassement.
    /// </summary>
    private decimal Conviction(decimal excess)
    {
        var span = BearishAbove - BullishBelow;
        return span <= 0m ? 1m : Math.Min(1m, excess / span);
    }
}
