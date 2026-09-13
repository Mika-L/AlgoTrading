using System.Collections;
using System.Collections.Frozen;

namespace AlgoTrading.Domain.MarketData;

/// <summary>
/// Historique d'un instrument, trié par date croissante et doublement indexé :
/// par position (<see cref="this[int]"/>) et par date (<see cref="TryGetIndex"/> en O(1),
/// <see cref="IndexAtOrBefore"/> par dichotomie).
/// <para>Les colonnes sont stockées à plat pour être exposées en
/// <see cref="ReadOnlySpan{T}"/> au noyau de calcul, sans allocation.</para>
/// </summary>
public sealed class BarSeries : IReadOnlyList<PriceBar>
{
    private readonly DateOnly[] _dates;
    private readonly decimal[] _open;
    private readonly decimal[] _high;
    private readonly decimal[] _low;
    private readonly decimal[] _close;
    private readonly decimal[] _rawClose;
    private readonly long[] _volume;
    private readonly FrozenDictionary<DateOnly, int> _byDate;

    private BarSeries(
        Symbol symbol,
        DateOnly[] dates,
        decimal[] open,
        decimal[] high,
        decimal[] low,
        decimal[] close,
        decimal[] rawClose,
        long[] volume)
    {
        Symbol = symbol;
        _dates = dates;
        _open = open;
        _high = high;
        _low = low;
        _close = close;
        _rawClose = rawClose;
        _volume = volume;

        var index = new Dictionary<DateOnly, int>(dates.Length);
        for (var i = 0; i < dates.Length; i++)
        {
            index[dates[i]] = i;
        }

        _byDate = index.ToFrozenDictionary();
    }

    public Symbol Symbol { get; }

    public int Count => _dates.Length;

    public bool IsEmpty => _dates.Length == 0;

    public DateOnly FirstDate => _dates.Length > 0 ? _dates[0] : throw new InvalidOperationException($"La série {Symbol} est vide.");

    public DateOnly LastDate => _dates.Length > 0 ? _dates[^1] : throw new InvalidOperationException($"La série {Symbol} est vide.");

    public PriceBar this[int index] =>
        new(_dates[index], _open[index], _high[index], _low[index], _close[index], _volume[index], _rawClose[index]);

    public ReadOnlySpan<DateOnly> Dates => _dates;
    public ReadOnlySpan<decimal> Open => _open;
    public ReadOnlySpan<decimal> High => _high;
    public ReadOnlySpan<decimal> Low => _low;
    public ReadOnlySpan<decimal> Close => _close;
    public ReadOnlySpan<decimal> RawClose => _rawClose;
    public ReadOnlySpan<long> Volume => _volume;

    /// <summary>Prix typiques <c>(H + L + C) / 3</c> de toute la série, alloués à la demande.</summary>
    public decimal[] TypicalPrices()
    {
        var result = new decimal[_dates.Length];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (_high[i] + _low[i] + _close[i]) / 3m;
        }

        return result;
    }

    /// <summary>Position de la séance à cette date exacte. O(1).</summary>
    public bool TryGetIndex(DateOnly date, out int index) => _byDate.TryGetValue(date, out index);

    /// <summary>
    /// Position de la dernière séance à une date inférieure ou égale, <c>-1</c> si la date
    /// précède le début de l'historique. Dichotomie en O(log n).
    /// </summary>
    public int IndexAtOrBefore(DateOnly date)
    {
        var lo = 0;
        var hi = _dates.Length - 1;
        var result = -1;

        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (_dates[mid] <= date)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return result;
    }

    /// <summary>Position de la première séance à une date supérieure ou égale, <c>-1</c> si aucune.</summary>
    public int IndexAtOrAfter(DateOnly date)
    {
        var at = IndexAtOrBefore(date);
        if (at >= 0 && _dates[at] == date)
        {
            return at;
        }

        var next = at + 1;
        return next < _dates.Length ? next : -1;
    }

    /// <summary>
    /// Construit une série à partir de barres déjà rétro-ajustées. Rejette les doublons
    /// de date et remet les barres en ordre croissant.
    /// </summary>
    public static BarSeries Create(Symbol symbol, IEnumerable<PriceBar> bars)
    {
        ArgumentNullException.ThrowIfNull(bars);

        var ordered = bars.OrderBy(static b => b.Date).ToArray();

        var dates = new DateOnly[ordered.Length];
        var open = new decimal[ordered.Length];
        var high = new decimal[ordered.Length];
        var low = new decimal[ordered.Length];
        var close = new decimal[ordered.Length];
        var rawClose = new decimal[ordered.Length];
        var volume = new long[ordered.Length];

        for (var i = 0; i < ordered.Length; i++)
        {
            var bar = ordered[i];
            if (i > 0 && bar.Date == dates[i - 1])
            {
                throw new ArgumentException($"La série {symbol} contient deux barres à la date {bar.Date:yyyy-MM-dd}.", nameof(bars));
            }

            dates[i] = bar.Date;
            open[i] = bar.Open;
            high[i] = bar.High;
            low[i] = bar.Low;
            close[i] = bar.Close;
            rawClose[i] = bar.RawClose;
            volume[i] = bar.Volume;
        }

        return new BarSeries(symbol, dates, open, high, low, close, rawClose, volume);
    }

    /// <summary>Sous-série bornée par les dates fournies, bornes incluses.</summary>
    public BarSeries Slice(DateOnly? from, DateOnly? to)
    {
        var start = from is { } f ? IndexAtOrAfter(f) : 0;
        if (start < 0)
        {
            return Create(Symbol, []);
        }

        var end = to is { } t ? IndexAtOrBefore(t) : _dates.Length - 1;
        if (end < start)
        {
            return Create(Symbol, []);
        }

        var slice = new PriceBar[end - start + 1];
        for (var i = 0; i < slice.Length; i++)
        {
            slice[i] = this[start + i];
        }

        return Create(Symbol, slice);
    }

    public IEnumerator<PriceBar> GetEnumerator()
    {
        for (var i = 0; i < _dates.Length; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() =>
        IsEmpty ? $"{Symbol} (vide)" : $"{Symbol} {FirstDate:yyyy-MM-dd}→{LastDate:yyyy-MM-dd} ({Count} séances)";
}
