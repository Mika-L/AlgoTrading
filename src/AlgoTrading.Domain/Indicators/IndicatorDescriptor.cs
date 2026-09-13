using System.Collections.Frozen;

namespace AlgoTrading.Domain.Indicators;

/// <summary>
/// Identité d'un calcul d'indicateur : une recette et ses paramètres, jamais des données.
/// <para>C'est ce qui débloque l'optimisation combinatoire : les résultats sont partageables
/// entre combinaisons puisque l'identité ne dépend plus de l'objet calculé. Et
/// <c>Rsi(period=14)</c> diffère de <c>Rsi(period=7)</c>, là où un filtrage par
/// <c>GetType()</c> confondait les deux et interdisait de les combiner.</para>
/// </summary>
public sealed record IndicatorDescriptor
{
    private readonly FrozenDictionary<string, decimal> _byName;

    public IndicatorDescriptor(string kind, IReadOnlyList<IndicatorParameter> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(parameters);

        Kind = kind;
        Parameters = [.. parameters.OrderBy(static p => p.Name, StringComparer.Ordinal)];
        _byName = Parameters.ToFrozenDictionary(static p => p.Name, static p => p.Value, StringComparer.Ordinal);
        Id = $"{Kind}({string.Join(',', Parameters)})";
    }

    public string Kind { get; }

    public IReadOnlyList<IndicatorParameter> Parameters { get; }

    /// <summary>Identité canonique, par exemple <c>Rsi(period=14)</c>. Clé du cache de calcul.</summary>
    public string Id { get; }

    public decimal this[string parameterName] => _byName[parameterName];

    public bool TryGetParameter(string name, out decimal value) => _byName.TryGetValue(name, out value);

    public static IndicatorDescriptor Of(string kind, params (string Name, decimal Value)[] parameters) =>
        new(kind, [.. parameters.Select(static p => new IndicatorParameter(p.Name, p.Value))]);

    public bool Equals(IndicatorDescriptor? other) => other is not null && Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Id;
}
