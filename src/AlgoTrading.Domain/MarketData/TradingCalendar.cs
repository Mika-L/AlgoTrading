namespace AlgoTrading.Domain.MarketData;

/// <summary>
/// Union ordonnée des séances de l'univers. Le moteur itère sur ces dates plutôt que sur
/// un calendrier civil : ~2 050 séances utiles au lieu de ~2 900 jours dont 40 % morts.
/// </summary>
public sealed class TradingCalendar
{
    private readonly DateOnly[] _sessions;

    private TradingCalendar(DateOnly[] sessions) => _sessions = sessions;

    public IReadOnlyList<DateOnly> Sessions => _sessions;

    public ReadOnlySpan<DateOnly> Span => _sessions;

    public int Count => _sessions.Length;

    public bool IsEmpty => _sessions.Length == 0;

    public DateOnly First => _sessions.Length > 0 ? _sessions[0] : throw new InvalidOperationException("Le calendrier est vide.");

    public DateOnly Last => _sessions.Length > 0 ? _sessions[^1] : throw new InvalidOperationException("Le calendrier est vide.");

    public DateOnly this[int index] => _sessions[index];

    public static TradingCalendar FromSeries(IEnumerable<BarSeries> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return FromDates(series.SelectMany(static s => s.Dates.ToArray()));
    }

    public static TradingCalendar FromDates(IEnumerable<DateOnly> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        return new TradingCalendar([.. dates.Distinct().Order()]);
    }

    /// <summary>Calendrier borné aux dates fournies, bornes incluses.</summary>
    public TradingCalendar Slice(DateOnly? from, DateOnly? to)
    {
        IEnumerable<DateOnly> kept = _sessions;

        if (from is { } f)
        {
            kept = kept.Where(d => d >= f);
        }

        if (to is { } t)
        {
            kept = kept.Where(d => d <= t);
        }

        return new TradingCalendar([.. kept]);
    }

    public override string ToString() =>
        IsEmpty ? "Calendrier vide" : $"{First:yyyy-MM-dd}→{Last:yyyy-MM-dd} ({Count} séances)";
}
