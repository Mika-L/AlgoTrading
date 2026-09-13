using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Ichimoku Kinko Hyo : Tenkan, Kijun et les deux bords du nuage.
/// <para>Le décalage des Senkou passe par <see cref="Series.Shift"/>, en barres. Le code
/// d'origine faisait <c>date.AddDays(26)</c> : la date obtenue tombait un week-end une fois
/// sur trois et la valeur devenait introuvable.</para>
/// <para>La Chikou Span n'est volontairement <b>pas</b> produite : lue à la barre <c>i</c>,
/// elle vaut la clôture de <c>i + 26</c>. C'est un look-ahead par construction, et
/// <see cref="Series.Shift"/> refuse d'ailleurs les décalages négatifs.</para>
/// </summary>
public sealed class Ichimoku : IndicatorBase
{
    public const string Kind = "Ichimoku";

    public Ichimoku(int tenkanPeriod = 9, int kijunPeriod = 26, int senkouBPeriod = 52, int displacement = 26)
    {
        TenkanPeriod = RequirePositive(tenkanPeriod, nameof(tenkanPeriod));
        KijunPeriod = RequirePositive(kijunPeriod, nameof(kijunPeriod));
        SenkouBPeriod = RequirePositive(senkouBPeriod, nameof(senkouBPeriod));
        ArgumentOutOfRangeException.ThrowIfNegative(displacement, nameof(displacement));
        Displacement = displacement;

        Descriptor = IndicatorDescriptor.Of(
            Kind,
            ("tenkan", tenkanPeriod),
            ("kijun", kijunPeriod),
            ("senkouB", senkouBPeriod),
            ("displacement", displacement));
    }

    public int TenkanPeriod { get; }

    public int KijunPeriod { get; }

    public int SenkouBPeriod { get; }

    public int Displacement { get; }

    public override IndicatorDescriptor Descriptor { get; }

    public override int WarmupBars => Math.Max(KijunPeriod - 1, SenkouBPeriod - 1 + Displacement);

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var (tenkan, tenkanFirst) = MidChannel(bars, TenkanPeriod);
        var (kijun, kijunFirst) = MidChannel(bars, KijunPeriod);
        var (senkouBRaw, senkouBRawFirst) = MidChannel(bars, SenkouBPeriod);

        var senkouARaw = new decimal[bars.Count];
        var senkouARawFirst = Math.Max(tenkanFirst, kijunFirst);
        for (var i = senkouARawFirst; i < bars.Count; i++)
        {
            senkouARaw[i] = (tenkan[i] + kijun[i]) / 2m;
        }

        var senkouA = Series.Shift(senkouARaw, Displacement, out var senkouAFirst, senkouARawFirst);
        var senkouB = Series.Shift(senkouBRaw, Displacement, out var senkouBFirst, senkouBRawFirst);

        return IndicatorResult.Create(Descriptor, bars.Count,
        [
            (IndicatorLines.TenkanSen, tenkan, tenkanFirst),
            (IndicatorLines.KijunSen, kijun, kijunFirst),
            (IndicatorLines.SenkouSpanA, senkouA, senkouAFirst),
            (IndicatorLines.SenkouSpanB, senkouB, senkouBFirst),
        ]);
    }

    /// <summary>Milieu du canal : moyenne du plus haut et du plus bas de la fenêtre.</summary>
    private static (decimal[] Values, int FirstValid) MidChannel(BarSeries bars, int period)
    {
        var highest = Series.HighestHigh(bars.High, period, out var firstValid);
        var lowest = Series.LowestLow(bars.Low, period, out _);

        var mid = new decimal[bars.Count];
        for (var i = firstValid; i < bars.Count; i++)
        {
            mid[i] = (highest[i] + lowest[i]) / 2m;
        }

        return (mid, firstValid);
    }
}
