using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators;

/// <summary>
/// Socle commun aux indicateurs : validation des paramètres à la construction — absente sur
/// 12 des 13 indicateurs d'origine — et gestion d'une série trop courte, qui produit un
/// résultat vide plutôt qu'une exception.
/// </summary>
public abstract class IndicatorBase : IIndicator
{
    public abstract IndicatorDescriptor Descriptor { get; }

    public abstract int WarmupBars { get; }

    public IndicatorResult Compute(BarSeries bars)
    {
        ArgumentNullException.ThrowIfNull(bars);
        return ComputeCore(bars);
    }

    protected abstract IndicatorResult ComputeCore(BarSeries bars);

    protected static int RequirePositive(int value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, parameterName);
        return value;
    }

    protected static decimal RequirePositive(decimal value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0m, parameterName);
        return value;
    }

    protected static decimal RequireNonNegative(decimal value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
        return value;
    }

    /// <summary>Résultat entièrement muet, pour une série plus courte que l'amorçage.</summary>
    protected IndicatorResult Empty(int count, params string[] lineNames) =>
        IndicatorResult.Create(Descriptor, count, [.. lineNames.Select(name => (name, new decimal[count], count))]);
}
