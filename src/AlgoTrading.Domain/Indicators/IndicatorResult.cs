using System.Collections.Frozen;

namespace AlgoTrading.Domain.Indicators;

/// <summary>
/// Résultat immuable d'un calcul d'indicateur : une ou plusieurs lignes nommées, chacune
/// avec son propre amorçage. Remplace le cache public mutable indexé par date, dont les
/// écritures sur <c>date.Date</c> et les lectures sur <c>date</c> divergeaient.
/// </summary>
public sealed class IndicatorResult
{
    private readonly FrozenDictionary<string, Line> _lines;

    private IndicatorResult(IndicatorDescriptor descriptor, int count, FrozenDictionary<string, Line> lines)
    {
        Descriptor = descriptor;
        Count = count;
        _lines = lines;
        LineNames = [.. lines.Keys.Order(StringComparer.Ordinal)];
        FirstValid = lines.Count == 0 ? count : lines.Values.Max(static l => l.FirstValid);
    }

    public IndicatorDescriptor Descriptor { get; }

    /// <summary>Nombre de barres couvertes — toujours celui de la série d'entrée.</summary>
    public int Count { get; }

    public IReadOnlyList<string> LineNames { get; }

    /// <summary>Index de la première barre où <b>toutes</b> les lignes sont signifiantes.</summary>
    public int FirstValid { get; }

    public ReadOnlySpan<decimal> this[string lineName] => _lines[lineName].Values;

    public int FirstValidOf(string lineName) => _lines[lineName].FirstValid;

    public bool HasLine(string lineName) => _lines.ContainsKey(lineName);

    /// <summary>
    /// Valeur de la ligne à cette barre, <c>false</c> si l'indicateur n'est pas encore amorcé.
    /// Aucune exception : un indicateur muet n'est pas une erreur.
    /// </summary>
    public bool TryGetValue(string lineName, int barIndex, out decimal value)
    {
        value = 0m;

        if (barIndex < 0 || barIndex >= Count || !_lines.TryGetValue(lineName, out var line) || barIndex < line.FirstValid)
        {
            return false;
        }

        value = line.Values[barIndex];
        return true;
    }

    public static IndicatorResult Single(IndicatorDescriptor descriptor, decimal[] values, int firstValid) =>
        Create(descriptor, values.Length, [(IndicatorLines.Value, values, firstValid)]);

    public static IndicatorResult Create(
        IndicatorDescriptor descriptor,
        int count,
        IReadOnlyList<(string Name, decimal[] Values, int FirstValid)> lines)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(lines);

        var built = new Dictionary<string, Line>(lines.Count, StringComparer.Ordinal);

        foreach (var (name, values, firstValid) in lines)
        {
            if (values.Length != count)
            {
                throw new ArgumentException($"La ligne « {name} » de {descriptor.Id} compte {values.Length} points pour {count} barres.", nameof(lines));
            }

            built[name] = new Line(values, Math.Clamp(firstValid, 0, count));
        }

        return new IndicatorResult(descriptor, count, built.ToFrozenDictionary(StringComparer.Ordinal));
    }

    private sealed record Line(decimal[] Values, int FirstValid);
}
