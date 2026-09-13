using System.Globalization;

namespace AlgoTrading.Domain.Indicators;

/// <summary>Un paramètre nommé d'indicateur, partie intégrante de son identité.</summary>
public sealed record IndicatorParameter(string Name, decimal Value)
{
    public string Name { get; } = string.IsNullOrWhiteSpace(Name)
        ? throw new ArgumentException("Un paramètre d'indicateur doit avoir un nom.", nameof(Name))
        : Name;

    /// <summary>Forme canonique <c>nom=valeur</c>, sans zéros décimaux superflus.</summary>
    public override string ToString() =>
        $"{Name}={Value.ToString("0.############################", CultureInfo.InvariantCulture)}";
}
