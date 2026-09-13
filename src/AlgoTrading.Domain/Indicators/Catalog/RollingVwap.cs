using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Prix moyen pondéré par les volumes sur une fenêtre <b>glissante</b>.
/// <para>Remplace le VWAP cumulatif depuis 2017, abandonné : cumulé sur huit ans, il
/// converge vers une constante et cesse de dire quoi que ce soit du marché du jour. Son
/// implémentation accumulait par ailleurs cinq défauts, dont un <c>TryGetValue</c> ignoré
/// qui faisait passer un VWAP introuvable pour un VWAP nul, donc un signal haussier à tort.</para>
/// </summary>
public sealed class RollingVwap : IndicatorBase
{
    public const string Kind = "Vwap";

    public RollingVwap(int period = 20)
    {
        Period = RequirePositive(period, nameof(period));
        Descriptor = IndicatorDescriptor.Of(Kind, ("period", period));
    }

    public int Period { get; }

    public override IndicatorDescriptor Descriptor { get; }

    public override int WarmupBars => Period - 1;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var count = bars.Count;
        if (count < Period)
        {
            return Empty(count, IndicatorLines.Value);
        }

        var typical = bars.TypicalPrices();
        var volume = bars.Volume;

        var vwap = new decimal[count];
        var firstValid = Period - 1;

        var weighted = 0m;
        var traded = 0m;
        var typicalSum = 0m;

        for (var i = 0; i < count; i++)
        {
            weighted += typical[i] * volume[i];
            traded += volume[i];
            typicalSum += typical[i];

            if (i >= Period)
            {
                var leaving = i - Period;
                weighted -= typical[leaving] * volume[leaving];
                traded -= volume[leaving];
                typicalSum -= typical[leaving];
            }

            if (i >= firstValid)
            {
                // Une fenêtre sans échange n'a pas de prix pondéré : on retombe sur la
                // moyenne simple des prix typiques plutôt que sur un zéro trompeur.
                vwap[i] = traded > 0m ? weighted / traded : typicalSum / Period;
            }
        }

        return IndicatorResult.Single(Descriptor, vwap, firstValid);
    }
}
