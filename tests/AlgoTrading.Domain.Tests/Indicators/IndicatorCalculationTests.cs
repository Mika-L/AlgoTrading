using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.Indicators.Catalog;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Indicators;

/// <summary>
/// Micro-séries construites pour que le résultat soit calculable de tête. Ce sont ces tests
/// qui attrapent une formule inversée ou un amorçage décalé d'un cran — ce qu'une
/// comparaison différentielle sur 500 barres aléatoires laisserait passer si les deux
/// implémentations partageaient la même erreur.
/// </summary>
public class IndicatorCalculationTests
{
    [Fact]
    public void should_collapse_the_keltner_channel_onto_its_average_when_the_multiplier_is_zero()
    {
        // Le test qui couvre d'un coup les deux composants faux du canal d'origine :
        // une moyenne arithmétique étiquetée « ema », et une largeur de canal calculée
        // sur High − Low au lieu de l'ATR.
        var bars = TestBars.Synthetic(count: 120);

        var flat = new KeltnerChannel(20, multiplier: 0m).Compute(bars);
        var reference = new ExponentialMovingAverage(19, 20).Compute(bars);

        for (var i = flat.FirstValid; i < bars.Count; i++)
        {
            flat.TryGetValue(IndicatorLines.Middle, i, out var middle).ShouldBeTrue();
            flat.TryGetValue(IndicatorLines.Upper, i, out var upper).ShouldBeTrue();
            flat.TryGetValue(IndicatorLines.Lower, i, out var lower).ShouldBeTrue();
            reference.TryGetValue(IndicatorLines.Slow, i, out var ema).ShouldBeTrue();

            middle.ShouldBe(ema, $"La ligne médiane de la barre {i} n'est pas la MME 20.");
            upper.ShouldBe(middle);
            lower.ShouldBe(middle);
        }
    }

    [Fact]
    public void should_centre_the_bollinger_bands_on_their_moving_average()
    {
        var bars = TestBars.Synthetic(count: 80);

        var bands = new BollingerBands(20, 2m).Compute(bars);

        for (var i = bands.FirstValid; i < bars.Count; i++)
        {
            bands.TryGetValue(IndicatorLines.Upper, i, out var upper).ShouldBeTrue();
            bands.TryGetValue(IndicatorLines.Middle, i, out var middle).ShouldBeTrue();
            bands.TryGetValue(IndicatorLines.Lower, i, out var lower).ShouldBeTrue();

            (upper - middle).ShouldBe(middle - lower, $"Les bandes de la barre {i} ne sont pas symétriques.");
            upper.ShouldBeGreaterThanOrEqualTo(middle);
        }
    }

    [Fact]
    public void should_leave_the_bollinger_bands_glued_to_the_average_on_a_flat_market()
    {
        var bars = TestBars.FromCloses([.. Enumerable.Repeat(50m, 30)]);

        var bands = new BollingerBands(20, 2m).Compute(bars);

        bands.TryGetValue(IndicatorLines.Upper, 25, out var upper).ShouldBeTrue();
        bands.TryGetValue(IndicatorLines.Lower, 25, out var lower).ShouldBeTrue();
        upper.ShouldBe(50m);
        lower.ShouldBe(50m);
    }

    [Fact]
    public void should_measure_the_momentum_as_the_gap_with_the_bar_of_the_period_before()
    {
        var bars = TestBars.FromCloses(10m, 11m, 12m, 13m, 14m, 20m);

        var momentum = new Momentum(3).Compute(bars);

        momentum.TryGetValue(IndicatorLines.Value, 3, out var value).ShouldBeTrue();
        value.ShouldBe(3m);   // 13 − 10
        momentum.TryGetValue(IndicatorLines.Value, 5, out value).ShouldBeTrue();
        value.ShouldBe(8m);   // 20 − 12
    }

    [Fact]
    public void should_place_the_stochastic_at_the_top_of_its_range_when_the_close_is_the_high()
    {
        // Clôtures croissantes : la dernière clôture est le plus haut de la fenêtre.
        var bars = TestBars.FromCloses([.. Enumerable.Range(1, 20).Select(i => (decimal)i)]);

        var stochastic = new StochasticOscillator(14).Compute(bars);

        stochastic.TryGetValue(IndicatorLines.Value, 19, out var k).ShouldBeTrue();
        k.ShouldBe(100m);
    }

    [Fact]
    public void should_place_williams_at_zero_when_the_close_is_the_high_of_the_window()
    {
        var bars = TestBars.FromCloses([.. Enumerable.Range(1, 20).Select(i => (decimal)i)]);

        var williams = new WilliamsPercentR(14).Compute(bars);

        williams.TryGetValue(IndicatorLines.Value, 19, out var value).ShouldBeTrue();
        value.ShouldBe(0m);
    }

    [Fact]
    public void should_weight_the_rolling_average_price_towards_the_busiest_session()
    {
        // Deux séances à 10 € sur 100 titres, une à 20 € sur 800 : la moyenne simple
        // vaudrait 13,33 €, la pondérée 18 €.
        var days = TestBars.BusinessDays(TestBars.Origin, 3);
        var bars = AlgoTrading.Domain.MarketData.BarSeries.Create(
            TestBars.DefaultSymbol,
            [
                new(days[0], 10m, 10m, 10m, 10m, 100L, 10m),
                new(days[1], 10m, 10m, 10m, 10m, 100L, 10m),
                new(days[2], 20m, 20m, 20m, 20m, 800L, 20m),
            ]);

        var vwap = new RollingVwap(3).Compute(bars);

        vwap.TryGetValue(IndicatorLines.Value, 2, out var value).ShouldBeTrue();
        value.ShouldBe(18m);   // (10·100 + 10·100 + 20·800) / 1000
    }

    [Fact]
    public void should_forget_the_oldest_session_once_it_leaves_the_rolling_window()
    {
        // Le VWAP cumulatif abandonné n'oubliait jamais rien : après huit ans il ne
        // disait plus rien du marché du jour.
        var bars = TestBars.FromCloses(1m, 1m, 1m, 100m, 100m);

        var vwap = new RollingVwap(2).Compute(bars);

        vwap.TryGetValue(IndicatorLines.Value, 4, out var value).ShouldBeTrue();
        value.ShouldBe(100m);
    }

    [Fact]
    public void should_flip_the_parabolic_stop_to_the_other_side_when_the_trend_reverses()
    {
        // Montée franche puis chute franche : le SAR doit passer sous le prix, puis au-dessus.
        var bars = TestBars.FromCloses(10m, 11m, 12m, 13m, 14m, 15m, 16m, 17m, 10m, 9m, 8m, 7m, 6m);

        var sar = new ParabolicSar().Compute(bars);

        sar.TryGetValue(IndicatorLines.Value, 7, out var rising).ShouldBeTrue();
        rising.ShouldBeLessThan(bars[7].Close);

        sar.TryGetValue(IndicatorLines.Value, 12, out var falling).ShouldBeTrue();
        falling.ShouldBeGreaterThan(bars[12].Close);
    }

    [Fact]
    public void should_push_the_ichimoku_cloud_forward_by_whole_bars()
    {
        // La Senkou A de la barre i vaut la moyenne Tenkan/Kijun calculée 26 barres plus tôt.
        var bars = TestBars.Synthetic(count: 200);

        var ichimoku = new Ichimoku(9, 26, 52, 26).Compute(bars);

        ichimoku.TryGetValue(IndicatorLines.TenkanSen, 100, out var tenkan).ShouldBeTrue();
        ichimoku.TryGetValue(IndicatorLines.KijunSen, 100, out var kijun).ShouldBeTrue();
        ichimoku.TryGetValue(IndicatorLines.SenkouSpanA, 126, out var senkouA).ShouldBeTrue();

        senkouA.ShouldBe((tenkan + kijun) / 2m);
    }

    [Fact]
    public void should_keep_the_average_true_range_positive_and_silent_before_its_period()
    {
        var bars = TestBars.Synthetic(count: 60);

        var atr = new AverageTrueRange(14).Compute(bars);

        atr.TryGetValue(IndicatorLines.Value, 13, out _).ShouldBeFalse();
        atr.TryGetValue(IndicatorLines.Value, 14, out var value).ShouldBeTrue();
        value.ShouldBeGreaterThan(0m);
    }

    [Fact]
    public void should_report_a_flat_channel_index_on_a_market_that_never_moves()
    {
        var bars = TestBars.FromCloses([.. Enumerable.Repeat(42m, 30)]);

        var cci = new CommodityChannelIndex(20).Compute(bars);

        cci.TryGetValue(IndicatorLines.Value, 25, out var value).ShouldBeTrue();
        value.ShouldBe(0m);
    }
}
