using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Reporting;

namespace AlgoTrading.Domain.Backtesting;

/// <summary>
/// La référence de toute stratégie : acheter l'univers à parts égales et ne plus rien faire.
/// Une stratégie qui ne la bat pas, sur les mêmes séances, n'apporte rien qu'un fonds
/// indiciel ne donne déjà.
/// </summary>
public static class BuyAndHold
{
    /// <summary>
    /// Courbe d'actif d'un portefeuille réparti à parts égales, à l'ouverture de la première
    /// séance, entre les titres qui cotent ce jour-là, puis conservé jusqu'à la dernière.
    /// <para>Sans frais : la référence est ainsi légèrement flattée, ce qui ne peut que rendre
    /// la comparaison plus exigeante pour la stratégie.</para>
    /// </summary>
    public static IReadOnlyList<EquityPoint> Curve(IReadOnlyList<BarSeries> universe, DateOnly from, DateOnly to, decimal initialCash)
    {
        ArgumentNullException.ThrowIfNull(universe);

        var calendar = TradingCalendar.FromSeries(universe).Slice(from, to);

        if (calendar.IsEmpty)
        {
            return [];
        }

        // Jamais vide : la première séance du calendrier est celle d'au moins un titre.
        var first = calendar.First;
        var holdings = universe.Where(s => s.TryGetIndex(first, out _)).ToArray();

        var budget = initialCash / holdings.Length;
        var shares = new decimal[holdings.Length];
        var marks = new decimal[holdings.Length];

        for (var i = 0; i < holdings.Length; i++)
        {
            holdings[i].TryGetIndex(first, out var opening);
            shares[i] = budget / holdings[i].Open[opening];
        }

        var curve = new List<EquityPoint>(calendar.Count);

        foreach (var date in calendar.Sessions)
        {
            var invested = 0m;

            for (var i = 0; i < holdings.Length; i++)
            {
                // Un titre qui ne cote pas ce jour-là garde sa dernière clôture.
                if (holdings[i].TryGetIndex(date, out var index))
                {
                    marks[i] = holdings[i].Close[index];
                }

                invested += shares[i] * marks[i];
            }

            curve.Add(new EquityPoint(date, invested, 0m, invested));
        }

        return curve;
    }
}
