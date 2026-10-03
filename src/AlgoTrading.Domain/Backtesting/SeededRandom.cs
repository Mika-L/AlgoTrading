namespace AlgoTrading.Domain.Backtesting;

/// <summary>
/// Générateur pseudo-aléatoire amorcé (SplitMix64). <c>System.Random</c> est banni du
/// domaine ; celui-ci exige une graine, et son algorithme, écrit ici, ne dépend d'aucune
/// version du runtime : même graine, même tirage, partout et pour toujours.
/// </summary>
public sealed class SeededRandom(ulong seed)
{
    private ulong _state = seed;

    public ulong NextUInt64()
    {
        var z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Un réel uniforme dans [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1d / (1UL << 53));

    /// <summary>Un entier uniforme dans [0, <paramref name="maxExclusive"/>).</summary>
    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxExclusive, 1);
        return (int)(NextUInt64() % (ulong)maxExclusive);
    }
}
