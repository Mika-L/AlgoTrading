namespace AlgoTrading.Domain.Indicators;

/// <summary>
/// Noyau de calcul partagé par tous les indicateurs. Remplace les 3 réécritures de l'EMA,
/// les 4 du plus-haut/plus-bas et les 7 fenêtres glissantes <c>Skip().Take()</c> en O(n·p).
/// <para>Convention : chaque fonction retourne un tableau de la longueur de la source et
/// publie par <c>out firstValid</c> l'index du premier élément signifiant. Les positions
/// antérieures valent <c>0</c> et ne doivent jamais être lues. Quand aucune valeur n'est
/// calculable, <c>firstValid</c> vaut la longueur de la source.</para>
/// <para><c>sourceFirstValid</c> permet le chaînage : l'EMA d'une ligne MACD qui ne commence
/// qu'au 26ᵉ point propage correctement son amorçage.</para>
/// </summary>
public static class Series
{
    /// <summary>Moyenne mobile simple sur <paramref name="period"/> points.</summary>
    public static decimal[] Sma(ReadOnlySpan<decimal> source, int period, out int firstValid, int sourceFirstValid = 0)
    {
        var result = Allocate(source, period, sourceFirstValid, out firstValid, out var start);
        if (start < 0)
        {
            return result;
        }

        var sum = 0m;
        for (var i = sourceFirstValid; i < start; i++)
        {
            sum += source[i];
        }

        for (var i = start; i < source.Length; i++)
        {
            sum += source[i];
            result[i] = sum / period;
            sum -= source[i - period + 1];
        }

        return result;
    }

    /// <summary>
    /// Moyenne mobile exponentielle, amorcée par la SMA des <paramref name="period"/> premiers
    /// points puis lissée au facteur <c>2 / (period + 1)</c>.
    /// </summary>
    public static decimal[] Ema(ReadOnlySpan<decimal> source, int period, out int firstValid, int sourceFirstValid = 0)
    {
        var result = Allocate(source, period, sourceFirstValid, out firstValid, out var start);
        if (start < 0)
        {
            return result;
        }

        var seed = 0m;
        for (var i = sourceFirstValid; i <= start; i++)
        {
            seed += source[i];
        }

        var previous = seed / period;
        result[start] = previous;

        var k = 2m / (period + 1);
        for (var i = start + 1; i < source.Length; i++)
        {
            previous += k * (source[i] - previous);
            result[i] = previous;
        }

        return result;
    }

    /// <summary>
    /// Lissage de Wilder : amorçage par la moyenne des <paramref name="period"/> premiers points,
    /// puis <c>prev + (x - prev) / period</c>. Partagé par le RSI et l'ATR — une seule
    /// implémentation là où le code initial en avait deux pour le même concept.
    /// </summary>
    public static decimal[] WilderSmooth(ReadOnlySpan<decimal> source, int period, out int firstValid, int sourceFirstValid = 0)
    {
        var result = Allocate(source, period, sourceFirstValid, out firstValid, out var start);
        if (start < 0)
        {
            return result;
        }

        var seed = 0m;
        for (var i = sourceFirstValid; i <= start; i++)
        {
            seed += source[i];
        }

        var previous = seed / period;
        result[start] = previous;

        for (var i = start + 1; i < source.Length; i++)
        {
            previous += (source[i] - previous) / period;
            result[i] = previous;
        }

        return result;
    }

    /// <summary>Écart type de population sur une fenêtre glissante.</summary>
    public static decimal[] StdDev(ReadOnlySpan<decimal> source, int period, out int firstValid, int sourceFirstValid = 0)
    {
        var result = Allocate(source, period, sourceFirstValid, out firstValid, out var start);
        if (start < 0)
        {
            return result;
        }

        for (var i = start; i < source.Length; i++)
        {
            var from = i - period + 1;

            var mean = 0m;
            for (var j = from; j <= i; j++)
            {
                mean += source[j];
            }

            mean /= period;

            var variance = 0m;
            for (var j = from; j <= i; j++)
            {
                var deviation = source[j] - mean;
                variance += deviation * deviation;
            }

            result[i] = Sqrt(variance / period);
        }

        return result;
    }

    /// <summary>Écart absolu moyen à la moyenne de la fenêtre — dénominateur du CCI.</summary>
    public static decimal[] MeanAbsoluteDeviation(ReadOnlySpan<decimal> source, int period, out int firstValid, int sourceFirstValid = 0)
    {
        var result = Allocate(source, period, sourceFirstValid, out firstValid, out var start);
        if (start < 0)
        {
            return result;
        }

        for (var i = start; i < source.Length; i++)
        {
            var from = i - period + 1;

            var mean = 0m;
            for (var j = from; j <= i; j++)
            {
                mean += source[j];
            }

            mean /= period;

            var deviation = 0m;
            for (var j = from; j <= i; j++)
            {
                deviation += Math.Abs(source[j] - mean);
            }

            result[i] = deviation / period;
        }

        return result;
    }

    /// <summary>
    /// Plus haut sur une fenêtre glissante, par deque monotone en O(n) — là où les quatre
    /// implémentations remplacées coûtaient O(n·p).
    /// </summary>
    public static decimal[] HighestHigh(ReadOnlySpan<decimal> source, int period, out int firstValid, int sourceFirstValid = 0) =>
        Extremum(source, period, sourceFirstValid, out firstValid, highest: true);

    /// <summary>Plus bas sur une fenêtre glissante, même deque monotone en O(n).</summary>
    public static decimal[] LowestLow(ReadOnlySpan<decimal> source, int period, out int firstValid, int sourceFirstValid = 0) =>
        Extremum(source, period, sourceFirstValid, out firstValid, highest: false);

    /// <summary>
    /// True range de Wilder : la plus grande des trois amplitudes — la séance, le gap haussier
    /// et le gap baissier face à la clôture précédente.
    /// <para>La première barre n'a <b>pas</b> de true range : il lui manque une clôture
    /// précédente. <c>firstValid</c> vaut donc 1, conformément à <i>New Concepts in Technical
    /// Trading Systems</i> — c'est ce qui fait tomber le premier ATR 14 sur la 15ᵉ barre.</para>
    /// </summary>
    public static decimal[] TrueRange(ReadOnlySpan<decimal> high, ReadOnlySpan<decimal> low, ReadOnlySpan<decimal> close, out int firstValid)
    {
        if (high.Length != low.Length || high.Length != close.Length)
        {
            throw new ArgumentException("Les colonnes haut, bas et clôture doivent avoir la même longueur.", nameof(low));
        }

        var result = new decimal[high.Length];
        if (high.Length == 0)
        {
            firstValid = 0;
            return result;
        }

        firstValid = Math.Min(1, high.Length);

        for (var i = 1; i < high.Length; i++)
        {
            var previousClose = close[i - 1];
            var range = high[i] - low[i];
            var upGap = Math.Abs(high[i] - previousClose);
            var downGap = Math.Abs(low[i] - previousClose);
            result[i] = Math.Max(range, Math.Max(upGap, downGap));
        }

        return result;
    }

    /// <summary>
    /// Décalage de <paramref name="barOffset"/> <b>barres</b> — jamais de jours calendaires.
    /// C'est la fonction qui supprime toute la classe de bugs <c>AddDays</c> : un croisement
    /// du vendredi reste détecté quand la barre suivante est un lundi.
    /// <para>Seul un décalage vers le passé est autorisé ; lire une barre postérieure serait
    /// un look-ahead par construction.</para>
    /// </summary>
    public static decimal[] Shift(ReadOnlySpan<decimal> source, int barOffset, out int firstValid, int sourceFirstValid = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(barOffset);

        var result = new decimal[source.Length];
        firstValid = Math.Min(sourceFirstValid + barOffset, source.Length);

        for (var i = firstValid; i < source.Length; i++)
        {
            result[i] = source[i - barOffset];
        }

        return result;
    }

    /// <summary>Variation sur <paramref name="lag"/> barres : <c>x[i] - x[i - lag]</c>.</summary>
    public static decimal[] Delta(ReadOnlySpan<decimal> source, int lag, out int firstValid, int sourceFirstValid = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lag, 1);

        var result = new decimal[source.Length];
        firstValid = Math.Min(sourceFirstValid + lag, source.Length);

        for (var i = firstValid; i < source.Length; i++)
        {
            result[i] = source[i] - source[i - lag];
        }

        return result;
    }

    /// <summary>Différence terme à terme de deux séries de même longueur.</summary>
    public static decimal[] Subtract(
        ReadOnlySpan<decimal> left,
        ReadOnlySpan<decimal> right,
        out int firstValid,
        int leftFirstValid = 0,
        int rightFirstValid = 0)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Les deux séries doivent avoir la même longueur.", nameof(right));
        }

        var result = new decimal[left.Length];
        firstValid = Math.Min(Math.Max(leftFirstValid, rightFirstValid), left.Length);

        for (var i = firstValid; i < left.Length; i++)
        {
            result[i] = left[i] - right[i];
        }

        return result;
    }

    /// <summary>Racine carrée d'un <see cref="decimal"/> par Newton-Raphson, amorcée en double.</summary>
    internal static decimal Sqrt(decimal value)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "La racine carrée d'un nombre négatif n'est pas définie.");
        }

        if (value == 0m)
        {
            return 0m;
        }

        var guess = (decimal)Math.Sqrt((double)value);
        if (guess <= 0m)
        {
            guess = value;
        }

        for (var i = 0; i < 16; i++)
        {
            var next = (guess + (value / guess)) / 2m;
            if (next == guess)
            {
                break;
            }

            guess = next;
        }

        return guess;
    }

    private static decimal[] Extremum(ReadOnlySpan<decimal> source, int period, int sourceFirstValid, out int firstValid, bool highest)
    {
        var result = Allocate(source, period, sourceFirstValid, out firstValid, out var start);
        if (start < 0)
        {
            return result;
        }

        // Deque monotone d'index : la tête porte toujours l'extrémum de la fenêtre courante.
        var deque = new int[source.Length - sourceFirstValid];
        var head = 0;
        var tail = 0;

        for (var i = sourceFirstValid; i < source.Length; i++)
        {
            while (tail > head && deque[head] <= i - period)
            {
                head++;
            }

            while (tail > head && (highest ? source[deque[tail - 1]] <= source[i] : source[deque[tail - 1]] >= source[i]))
            {
                tail--;
            }

            deque[tail++] = i;

            if (i >= start)
            {
                result[i] = source[deque[head]];
            }
        }

        return result;
    }

    /// <summary>
    /// Alloue le résultat et calcule l'amorçage commun. <c>start</c> vaut <c>-1</c> quand la
    /// source est trop courte pour produire la moindre valeur.
    /// </summary>
    private static decimal[] Allocate(ReadOnlySpan<decimal> source, int period, int sourceFirstValid, out int firstValid, out int start)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceFirstValid);

        var result = new decimal[source.Length];
        var candidate = sourceFirstValid + period - 1;

        if (candidate >= source.Length)
        {
            firstValid = source.Length;
            start = -1;
            return result;
        }

        firstValid = candidate;
        start = candidate;
        return result;
    }
}
