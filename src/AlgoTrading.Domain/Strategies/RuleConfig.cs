using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.Strategies.Rules;

namespace AlgoTrading.Domain.Strategies;

/// <summary>
/// Description sérialisable d'une règle. Les stratégies vivent désormais dans des fichiers,
/// plus dans quatre-vingts lignes de lambdas commentées référençant des classes inexistantes.
/// <para>Les réglages sont des champs nommés et non un sac de valeurs : un fichier de
/// stratégie se relit sans consulter le code.</para>
/// </summary>
public sealed record RuleConfig
{
    /// <summary>Type de règle : <c>Threshold</c>, <c>Crossover</c>, <c>BandBreakout</c>, <c>PriceVsLevel</c>, <c>Sign</c>, <c>IchimokuCloud</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Nom de l'indicateur, par exemple <c>Rsi</c>.</summary>
    public required string Indicator { get; init; }

    /// <summary>Paramètres de l'indicateur ; ceux qui manquent prennent leur valeur par défaut.</summary>
    public Dictionary<string, decimal> Parameters { get; init; } = [];

    /// <summary>Poids de la règle dans une agrégation pondérée.</summary>
    public decimal Weight { get; init; } = 1m;

    /// <summary>Convertit une règle d'état en règle d'événement — voir <see cref="EdgeRule"/>.</summary>
    public bool AsEvent { get; init; }

    public decimal? BullishBelow { get; init; }

    public decimal? BearishAbove { get; init; }

    public string? Line { get; init; }

    public string? FastLine { get; init; }

    public string? SlowLine { get; init; }

    public CrossoverMode? Mode { get; init; }

    public bool? MeanReverting { get; init; }

    public bool? BullishWhenAbove { get; init; }

    public decimal? Pivot { get; init; }

    /// <summary>Descripteur de l'indicateur tel que le construira le catalogue.</summary>
    public IndicatorDescriptor ToDescriptor() =>
        new(Indicator, [.. Parameters.Select(static p => new IndicatorParameter(p.Key, p.Value))]);
}
