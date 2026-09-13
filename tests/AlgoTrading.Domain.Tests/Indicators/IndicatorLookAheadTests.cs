using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Indicators;

/// <summary>
/// Le test décisif du projet, écrit une fois pour le catalogue entier : calculer sur
/// l'historique tronqué à la barre <c>t</c> doit donner, en <c>t</c>, exactement la même
/// valeur que calculer sur l'historique complet.
/// <para>Il aurait attrapé le look-ahead de l'ATR, qui comparait la volatilité du jour à la
/// moyenne de toute la série — futur compris — et invalidait tout backtest l'incluant.</para>
/// </summary>
public class IndicatorLookAheadTests
{
    public static TheoryData<string> Catalog =>
        [.. IndicatorCatalog.Defaults.Select(static i => i.Descriptor.Id)];

    [Theory]
    [MemberData(nameof(Catalog))]
    public void should_never_let_an_indicator_depend_on_a_bar_later_than_the_day_it_values(string descriptorId)
    {
        var indicator = IndicatorCatalog.Defaults.Single(i => i.Descriptor.Id == descriptorId);
        var full = TestBars.Synthetic(count: 260);

        var complete = indicator.Compute(full);

        // Trois points d'observation : juste après l'amorçage, au milieu, et près de la fin.
        foreach (var t in ObservationPoints(indicator.WarmupBars, full.Count))
        {
            var truncated = indicator.Compute(Truncate(full, t));

            foreach (var line in complete.LineNames)
            {
                var seenLive = truncated.TryGetValue(line, t, out var live);
                var seenWithHindsight = complete.TryGetValue(line, t, out var hindsight);

                seenLive.ShouldBe(
                    seenWithHindsight,
                    $"{descriptorId}, ligne « {line} » : la barre {t} n'a pas le même statut selon que le futur est connu ou non.");

                if (seenLive)
                {
                    live.ShouldBe(
                        hindsight,
                        $"{descriptorId}, ligne « {line} » : la barre {t} vaut {live} en temps réel et {hindsight} avec le futur.");
                }
            }
        }
    }

    private static IEnumerable<int> ObservationPoints(int warmup, int count)
    {
        var first = Math.Min(warmup + 1, count - 1);
        yield return first;
        yield return (first + count - 1) / 2;
        yield return count - 1;
    }

    private static BarSeries Truncate(BarSeries series, int lastIndex)
    {
        var kept = new PriceBar[lastIndex + 1];
        for (var i = 0; i <= lastIndex; i++)
        {
            kept[i] = series[i];
        }

        return BarSeries.Create(series.Symbol, kept);
    }
}
