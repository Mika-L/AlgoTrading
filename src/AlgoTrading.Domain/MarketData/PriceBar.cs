namespace AlgoTrading.Domain.MarketData;

/// <summary>
/// Une séance, entièrement rétro-ajustée : O/H/L/C portent déjà le facteur
/// <c>AdjustedClose / Close</c>, le volume son inverse. Aucun indicateur ne peut donc
/// mélanger deux échelles de prix — le défaut disparaît à la source.
/// <para><see cref="RawClose"/> conserve la clôture non ajustée, à seule fin de traçabilité.</para>
/// <para>La date est une <see cref="DateOnly"/> : la divergence <c>date</c> / <c>date.Date</c>
/// devient inexprimable.</para>
/// </summary>
public sealed record PriceBar
{
    public PriceBar(DateOnly date, decimal open, decimal high, decimal low, decimal close, long volume, decimal rawClose)
    {
        if (open <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(open), open, "L'ouverture doit être strictement positive.");
        }

        if (high <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(high), high, "Le plus haut doit être strictement positif.");
        }

        if (low <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(low), low, "Le plus bas doit être strictement positif.");
        }

        if (close <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(close), close, "La clôture doit être strictement positive.");
        }

        if (volume < 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), volume, "Le volume ne peut pas être négatif.");
        }

        if (high < low)
        {
            throw new ArgumentException($"Barre {date:yyyy-MM-dd} incohérente : plus haut {high} < plus bas {low}.", nameof(high));
        }

        if (high < open || high < close)
        {
            throw new ArgumentException($"Barre {date:yyyy-MM-dd} incohérente : plus haut {high} inférieur à l'ouverture {open} ou à la clôture {close}.", nameof(high));
        }

        if (low > open || low > close)
        {
            throw new ArgumentException($"Barre {date:yyyy-MM-dd} incohérente : plus bas {low} supérieur à l'ouverture {open} ou à la clôture {close}.", nameof(low));
        }

        Date = date;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        RawClose = rawClose;
    }

    public DateOnly Date { get; }
    public decimal Open { get; }
    public decimal High { get; }
    public decimal Low { get; }
    public decimal Close { get; }
    public long Volume { get; }

    /// <summary>Clôture non ajustée, telle que publiée. Ne sert jamais à un calcul d'indicateur.</summary>
    public decimal RawClose { get; }

    /// <summary>Prix typique <c>(H + L + C) / 3</c>, base du CCI et du VWAP.</summary>
    public decimal Typical => (High + Low + Close) / 3m;

    public decimal Range => High - Low;

    public override string ToString() =>
        $"{Date:yyyy-MM-dd} O={Open} H={High} L={Low} C={Close} V={Volume}";
}
