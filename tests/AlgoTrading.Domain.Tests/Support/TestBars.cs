using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Tests.Support;

/// <summary>
/// Générateur de séries de test en jours <b>ouvrés</b>. Les week-ends sont sautés : c'est ce
/// qui rend testable la non-régression du bug du lundi — un décalage exprimé en jours
/// calendaires y échoue, un décalage en barres y réussit.
/// </summary>
public static class TestBars
{
    /// <summary>Lundi 2 janvier 2017, point de départ par défaut de toutes les séries de test.</summary>
    public static readonly DateOnly Origin = new(2017, 1, 2);

    public static Symbol DefaultSymbol { get; } = Symbol.From("TEST");

    /// <summary>Les <paramref name="count"/> jours ouvrés à partir de <paramref name="start"/> inclus.</summary>
    public static DateOnly[] BusinessDays(DateOnly start, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var days = new DateOnly[count];
        var cursor = start;

        for (var i = 0; i < count; i++)
        {
            while (cursor.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                cursor = cursor.AddDays(1);
            }

            days[i] = cursor;
            cursor = cursor.AddDays(1);
        }

        return days;
    }

    /// <summary>Série plate où ouverture, plus haut, plus bas et clôture valent la même valeur.</summary>
    public static BarSeries FromCloses(params decimal[] closes) => FromCloses(DefaultSymbol, Origin, closes);

    public static BarSeries FromCloses(Symbol symbol, DateOnly start, params decimal[] closes)
    {
        var days = BusinessDays(start, closes.Length);
        var bars = new PriceBar[closes.Length];

        for (var i = 0; i < closes.Length; i++)
        {
            bars[i] = new PriceBar(days[i], closes[i], closes[i], closes[i], closes[i], 1_000L, closes[i]);
        }

        return BarSeries.Create(symbol, bars);
    }

    /// <summary>Série à OHLC explicites, une entrée par séance.</summary>
    public static BarSeries FromOhlc(params (decimal Open, decimal High, decimal Low, decimal Close)[] bars) =>
        FromOhlc(DefaultSymbol, Origin, bars);

    public static BarSeries FromOhlc(Symbol symbol, DateOnly start, params (decimal Open, decimal High, decimal Low, decimal Close)[] bars)
    {
        var days = BusinessDays(start, bars.Length);
        var priceBars = new PriceBar[bars.Length];

        for (var i = 0; i < bars.Length; i++)
        {
            var (open, high, low, close) = bars[i];
            priceBars[i] = new PriceBar(days[i], open, high, low, close, 1_000L, close);
        }

        return BarSeries.Create(symbol, priceBars);
    }

    /// <summary>
    /// Série pseudo-aléatoire reproductible : même graine, mêmes barres, à toute exécution.
    /// Sert aux comparaisons différentielles contre une bibliothèque de référence.
    /// </summary>
    public static BarSeries Synthetic(int count, int seed = 1789, decimal start = 100m)
    {
        var random = new Random(seed);
        var days = BusinessDays(Origin, count);
        var bars = new PriceBar[count];
        var close = start;

        for (var i = 0; i < count; i++)
        {
            var drift = (decimal)((random.NextDouble() - 0.48) * 2.0);
            var open = decimal.Round(Math.Max(1m, close), 4);
            close = decimal.Round(Math.Max(1m, open + drift), 4);
            var high = decimal.Round(Math.Max(open, close) + (decimal)(random.NextDouble() * 0.9), 4);
            var low = decimal.Round(Math.Max(0.5m, Math.Min(open, close) - (decimal)(random.NextDouble() * 0.9)), 4);
            var volume = 100_000L + random.Next(0, 900_000);

            bars[i] = new PriceBar(days[i], open, high, low, close, volume, close);
        }

        return BarSeries.Create(DefaultSymbol, bars);
    }
}
