using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.Indicators.Catalog;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Indicators;

/// <summary>
/// Propriétés que les 13 indicateurs doivent tenir collectivement. La validation des
/// paramètres, notamment, n'existait que sur le RSI : les douze autres acceptaient une
/// période nulle ou négative sans broncher.
/// </summary>
public class IndicatorContractTests
{
    public static TheoryData<string> Catalog => [.. IndicatorCatalog.Defaults.Select(static i => i.Descriptor.Id)];

    [Theory]
    [MemberData(nameof(Catalog))]
    public void should_stay_silent_rather_than_fail_on_a_history_shorter_than_its_warmup(string descriptorId)
    {
        var indicator = IndicatorCatalog.Defaults.Single(i => i.Descriptor.Id == descriptorId);
        var tooShort = TestBars.FromCloses(10m, 11m, 12m);

        var result = indicator.Compute(tooShort);

        result.Count.ShouldBe(tooShort.Count);
        result.TryGetValue(IndicatorLines.Value, 0, out _).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Catalog))]
    public void should_produce_values_once_past_its_announced_warmup(string descriptorId)
    {
        var indicator = IndicatorCatalog.Defaults.Single(i => i.Descriptor.Id == descriptorId);
        var bars = TestBars.Synthetic(count: 300);

        var result = indicator.Compute(bars);

        result.FirstValid.ShouldBeLessThanOrEqualTo(
            indicator.WarmupBars,
            $"{descriptorId} annonce un amorçage de {indicator.WarmupBars} barres mais ne produit rien avant la barre {result.FirstValid}.");
    }

    [Theory]
    [MemberData(nameof(Catalog))]
    public void should_give_the_same_result_when_computed_twice(string descriptorId)
    {
        var indicator = IndicatorCatalog.Defaults.Single(i => i.Descriptor.Id == descriptorId);
        var bars = TestBars.Synthetic(count: 200);

        var first = indicator.Compute(bars);
        var second = indicator.Compute(bars);

        foreach (var line in first.LineNames)
        {
            for (var i = 0; i < bars.Count; i++)
            {
                first.TryGetValue(line, i, out var a).ShouldBe(second.TryGetValue(line, i, out var b));
                a.ShouldBe(b);
            }
        }
    }

    [Fact]
    public void should_reject_a_period_that_is_not_strictly_positive_on_every_indicator()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ExponentialMovingAverage(0, 20));
        Should.Throw<ArgumentOutOfRangeException>(() => new Macd(0, 26, 9));
        Should.Throw<ArgumentOutOfRangeException>(() => new Momentum(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new RelativeStrengthIndex(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new CommodityChannelIndex(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new StochasticOscillator(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new WilliamsPercentR(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new AverageTrueRange(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new BollingerBands(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new KeltnerChannel(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new Ichimoku(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new ParabolicSar(0m));
        Should.Throw<ArgumentOutOfRangeException>(() => new RollingVwap(0));
    }

    [Fact]
    public void should_refuse_a_short_average_that_is_not_shorter_than_the_long_one()
    {
        Should.Throw<ArgumentException>(() => new ExponentialMovingAverage(20, 20));
        Should.Throw<ArgumentException>(() => new Macd(26, 12, 9));
    }

    [Fact]
    public void should_refuse_an_acceleration_above_its_own_ceiling()
    {
        Should.Throw<ArgumentException>(() => new ParabolicSar(0.5m, 0.2m));
    }

    [Fact]
    public void should_tell_two_settings_of_the_same_indicator_apart()
    {
        // C'est ce qui redonne le droit de combiner RSI(14) et RSI(7) : leur identité
        // ne vient plus de leur type C# mais de leurs paramètres.
        var fourteen = new RelativeStrengthIndex(14).Descriptor;
        var seven = new RelativeStrengthIndex(7).Descriptor;

        fourteen.Id.ShouldBe("Rsi(period=14)");
        seven.Id.ShouldBe("Rsi(period=7)");
        fourteen.ShouldNotBe(seven);
    }

    [Fact]
    public void should_rebuild_any_indicator_from_its_descriptor_alone()
    {
        foreach (var original in IndicatorCatalog.Defaults)
        {
            var rebuilt = IndicatorCatalog.Create(original.Descriptor);

            rebuilt.Descriptor.ShouldBe(original.Descriptor);
            rebuilt.WarmupBars.ShouldBe(original.WarmupBars);
        }
    }

    [Fact]
    public void should_reject_an_unknown_indicator_name()
    {
        var unknown = IndicatorDescriptor.Of("Divination", ("period", 14));

        Should.Throw<ArgumentException>(() => IndicatorCatalog.Create(unknown));
    }
}
