using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.Indicators.Catalog;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;
using Skender.Stock.Indicators;

namespace AlgoTrading.Domain.Tests.Indicators;

/// <summary>
/// Comparaison différentielle contre une implémentation de référence indépendante, sur
/// 500 barres synthétiques figées. C'est ce qui donne leur crédibilité aux indicateurs dont
/// la formule est trop longue pour être vérifiée de tête.
/// <para>Toute divergence intentionnelle est documentée dans le test qui la porte.</para>
/// </summary>
public class SkenderDifferentialTests
{
    private static readonly BarSeries Bars = TestBars.Synthetic(count: 500);
    private static readonly IReadOnlyList<Quote> Quotes = SkenderBridge.ToQuotes(Bars);
    /// <summary>Nos calculs sont en decimal de bout en bout ; la référence aussi sur ces lignes.</summary>
    private const decimal ExactTolerance = 0.00000001m;

    /// <summary>
    /// La référence calcule certaines lignes en double. La tolérance couvre alors l'erreur
    /// d'arrondi accumulée sur 500 barres par un lissage exponentiel, pas un écart de formule.
    /// </summary>
    private const decimal DoubleTolerance = 0.000001m;

    [Fact]
    public void should_match_the_reference_relative_strength_index()
    {
        var ours = new RelativeStrengthIndex(14).Compute(Bars);
        var theirs = Quotes.GetRsi(14).ToList();

        AssertSameLine(ours, IndicatorLines.Value, theirs.Select(static r => (decimal?)r.Rsi));
    }

    [Fact]
    public void should_match_the_reference_average_true_range()
    {
        var ours = new AverageTrueRange(14).Compute(Bars);
        var theirs = Quotes.GetAtr(14).ToList();

        AssertSameLine(ours, IndicatorLines.Value, theirs.Select(static r => (decimal?)r.Atr));
    }

    [Fact]
    public void should_match_the_reference_bollinger_bands()
    {
        var ours = new BollingerBands(20, 2m).Compute(Bars);
        var theirs = Quotes.GetBollingerBands(20, 2).ToList();

        AssertSameLine(ours, IndicatorLines.Middle, theirs.Select(static r => r.Sma));
        AssertSameLine(ours, IndicatorLines.Upper, theirs.Select(static r => r.UpperBand));
        AssertSameLine(ours, IndicatorLines.Lower, theirs.Select(static r => r.LowerBand));
    }

    [Fact]
    public void should_match_the_reference_stochastic_oscillator()
    {
        var ours = new StochasticOscillator(14, 3).Compute(Bars);
        // smoothPeriods = 1 pour obtenir le %K brut : avec 3, la référence renvoie un
        // stochastique « lent », déjà lissé, qui ne correspond pas à notre ligne « value ».
        var theirs = Quotes.GetStoch(14, 3, 1).ToList();

        AssertSameLine(ours, IndicatorLines.Value, theirs.Select(static r => r.Oscillator));
        AssertSameLine(ours, IndicatorLines.Signal, theirs.Select(static r => r.Signal));
    }

    [Fact]
    public void should_match_the_reference_ichimoku_cloud()
    {
        var ours = new Ichimoku(9, 26, 52, 26).Compute(Bars);
        var theirs = Quotes.GetIchimoku(9, 26, 52).ToList();

        AssertSameLine(ours, IndicatorLines.TenkanSen, theirs.Select(static r => r.TenkanSen));
        AssertSameLine(ours, IndicatorLines.KijunSen, theirs.Select(static r => r.KijunSen));
        AssertSameLine(ours, IndicatorLines.SenkouSpanA, theirs.Select(static r => r.SenkouSpanA));
        AssertSameLine(ours, IndicatorLines.SenkouSpanB, theirs.Select(static r => r.SenkouSpanB));
    }

    [Fact]
    public void should_match_the_reference_parabolic_sar()
    {
        var ours = new ParabolicSar(0.02m, 0.2m).Compute(Bars);
        var theirs = Quotes.GetParabolicSar(0.02, 0.2).ToList();

        AssertSameLine(ours, IndicatorLines.Value, theirs.Select(static r => r.Sar));
    }

    [Fact]
    public void should_match_the_reference_commodity_channel_index()
    {
        var ours = new CommodityChannelIndex(20).Compute(Bars);
        var theirs = Quotes.GetCci(20).ToList();

        AssertSameLine(ours, IndicatorLines.Value, theirs.Select(static r => r.Cci));
    }

    [Fact]
    public void should_match_the_reference_williams_percent_r()
    {
        var ours = new WilliamsPercentR(14).Compute(Bars);
        var theirs = Quotes.GetWilliamsR(14).ToList();

        AssertSameLine(ours, IndicatorLines.Value, theirs.Select(static r => r.WilliamsR));
    }

    [Fact]
    public void should_match_the_reference_macd()
    {
        var ours = new Macd(12, 26, 9).Compute(Bars);
        var theirs = Quotes.GetMacd(12, 26, 9).ToList();

        AssertSameLine(ours, IndicatorLines.Value, theirs.Select(static r => r.Macd));
        AssertSameLine(ours, IndicatorLines.Signal, theirs.Select(static r => r.Signal));
    }

    /// <summary>
    /// Compare ligne à ligne. Les barres où la référence ne produit rien sont ignorées :
    /// les conventions d'amorçage diffèrent d'une bibliothèque à l'autre, c'est la seule
    /// divergence tolérée.
    /// </summary>
    private static void AssertSameLine(IndicatorResult ours, string lineName, IEnumerable<decimal?> reference)
    {
        var expected = reference.ToList();
        expected.Count.ShouldBe(ours.Count);

        var compared = 0;

        for (var i = 0; i < expected.Count; i++)
        {
            if (expected[i] is not { } theirs || !ours.TryGetValue(lineName, i, out var mine))
            {
                continue;
            }

            mine.ShouldBe(theirs, ExactTolerance, $"Ligne « {lineName} », barre {i}.");
            compared++;
        }

        compared.ShouldBeGreaterThan(100, $"La ligne « {lineName} » n'a été confrontée que sur {compared} barres.");
    }

    /// <summary>Même comparaison, pour les lignes que la référence produit en double.</summary>
    private static void AssertSameLine(IndicatorResult ours, string lineName, IEnumerable<double?> reference)
    {
        var expected = reference.ToList();
        expected.Count.ShouldBe(ours.Count);

        var compared = 0;

        for (var i = 0; i < expected.Count; i++)
        {
            if (expected[i] is not { } theirs || !ours.TryGetValue(lineName, i, out var mine))
            {
                continue;
            }

            mine.ShouldBe((decimal)theirs, DoubleTolerance, $"Ligne « {lineName} », barre {i}.");
            compared++;
        }

        compared.ShouldBeGreaterThan(100, $"La ligne « {lineName} » n'a été confrontée que sur {compared} barres.");
    }
}
