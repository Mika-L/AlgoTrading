namespace AlgoTrading.Domain.MarketData;

/// <summary>
/// Identifiant d'un instrument. Remplace les <c>Dictionary&lt;Stock, …&gt;</c> à clés par
/// référence, dont l'ordre d'énumération déterminait le résultat du backtest :
/// <see cref="IComparable{T}"/> donne un ordre d'itération déterministe.
/// </summary>
public readonly record struct Symbol : IComparable<Symbol>, IComparable
{
    private readonly string? _ticker;

    public Symbol(string ticker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);
        _ticker = ticker.Trim().ToUpperInvariant();
    }

    /// <summary>Le ticker normalisé (majuscules, sans espaces), par exemple <c>GLE.PA</c>.</summary>
    public string Ticker => _ticker ?? string.Empty;

    public bool IsEmpty => string.IsNullOrEmpty(_ticker);

    public static Symbol From(string ticker) => new(ticker);

    public int CompareTo(Symbol other) => string.CompareOrdinal(Ticker, other.Ticker);

    int IComparable.CompareTo(object? obj) =>
        obj is Symbol other ? CompareTo(other) : throw new ArgumentException($"Objet de type {obj?.GetType().Name ?? "null"} incomparable à un Symbol.", nameof(obj));

    public static bool operator <(Symbol left, Symbol right) => left.CompareTo(right) < 0;
    public static bool operator <=(Symbol left, Symbol right) => left.CompareTo(right) <= 0;
    public static bool operator >(Symbol left, Symbol right) => left.CompareTo(right) > 0;
    public static bool operator >=(Symbol left, Symbol right) => left.CompareTo(right) >= 0;

    public override string ToString() => Ticker;
}
