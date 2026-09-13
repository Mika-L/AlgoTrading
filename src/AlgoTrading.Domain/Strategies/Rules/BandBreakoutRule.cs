using AlgoTrading.Domain.Indicators;

namespace AlgoTrading.Domain.Strategies.Rules;

/// <summary>
/// Sortie de la clôture hors d'un canal — Bollinger, Keltner.
/// <para><see cref="MeanReverting"/> acte un mélange jusqu'ici implicite : acheter sous la
/// bande basse est un pari de <b>retour à la moyenne</b>, alors qu'acheter un croisement
/// haussier de moyennes est un pari de <b>suivi de tendance</b>. Le code d'origine faisait
/// les deux dans le même vote majoritaire sans le dire.</para>
/// </summary>
public sealed class BandBreakoutRule : ISignalRule
{
    public const string Type = "BandBreakout";

    public BandBreakoutRule(IndicatorDescriptor indicator, bool meanReverting = true)
    {
        ArgumentNullException.ThrowIfNull(indicator);

        Indicator = indicator;
        MeanReverting = meanReverting;
        WarmupBars = IndicatorCatalog.Create(indicator).WarmupBars;
    }

    /// <summary>Vrai : sous la bande basse on achète. Faux : on vend, et on achète les sorties par le haut.</summary>
    public bool MeanReverting { get; }

    public IndicatorDescriptor Indicator { get; }

    public SignalKind Kind => SignalKind.State;

    public int WarmupBars { get; }

    public string Name => MeanReverting
        ? $"{Indicator.Id} : retour à la moyenne hors du canal"
        : $"{Indicator.Id} : sortie de canal suivie";

    public Signal Evaluate(in RuleContext context)
    {
        if (!context.TryGetValue(IndicatorLines.Upper, out var upper) ||
            !context.TryGetValue(IndicatorLines.Lower, out var lower))
        {
            return Signal.None;
        }

        var close = context.Close;

        if (close < lower)
        {
            return MeanReverting ? Signal.Bullish() : Signal.Bearish();
        }

        if (close > upper)
        {
            return MeanReverting ? Signal.Bearish() : Signal.Bullish();
        }

        return Signal.None;
    }
}
