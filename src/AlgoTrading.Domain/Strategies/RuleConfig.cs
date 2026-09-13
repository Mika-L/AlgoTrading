using System.Text.Json.Serialization;
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
    public const decimal DefaultWeight = 1m;

    public RuleConfig()
    {
    }

    /// <summary>
    /// Constructeur de désérialisation. Il est nécessaire : le générateur de sérialisation
    /// n'exécute <b>pas</b> les initialiseurs de propriétés, et un réglage absent du JSON
    /// retomberait silencieusement à zéro — un poids nul, un seuil nul, des frais nuls.
    /// Seules les valeurs par défaut de <b>paramètres de constructeur</b> sont honorées.
    /// </summary>
    [JsonConstructor]
    public RuleConfig(
        string type,
        string indicator,
        Dictionary<string, decimal>? parameters = null,
        decimal weight = DefaultWeight,
        bool asEvent = false,
        decimal? bullishBelow = null,
        decimal? bearishAbove = null,
        string? line = null,
        string? fastLine = null,
        string? slowLine = null,
        CrossoverMode? mode = null,
        bool? meanReverting = null,
        bool? bullishWhenAbove = null,
        decimal? pivot = null)
    {
        Type = type;
        Indicator = indicator;
        Parameters = parameters ?? [];
        Weight = weight;
        AsEvent = asEvent;
        BullishBelow = bullishBelow;
        BearishAbove = bearishAbove;
        Line = line;
        FastLine = fastLine;
        SlowLine = slowLine;
        Mode = mode;
        MeanReverting = meanReverting;
        BullishWhenAbove = bullishWhenAbove;
        Pivot = pivot;
    }

    /// <summary>Type de règle : <c>Threshold</c>, <c>Crossover</c>, <c>BandBreakout</c>, <c>PriceVsLevel</c>, <c>Sign</c>, <c>IchimokuCloud</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Nom de l'indicateur, par exemple <c>Rsi</c>.</summary>
    public required string Indicator { get; init; }

    /// <summary>Paramètres de l'indicateur ; ceux qui manquent prennent leur valeur par défaut.</summary>
    public Dictionary<string, decimal> Parameters { get; init; } = [];

    /// <summary>Poids de la règle dans une agrégation pondérée.</summary>
    public decimal Weight { get; init; } = DefaultWeight;

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
