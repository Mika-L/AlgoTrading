using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlgoTrading.Domain.Strategies;

/// <summary>
/// Contexte de sérialisation généré à la compilation : pas de réflexion à l'exécution,
/// et les énumérations s'écrivent en toutes lettres dans les fichiers de stratégie.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(StrategyDefinition))]
[JsonSerializable(typeof(SignalPolicy))]
[JsonSerializable(typeof(RuleConfig))]
[JsonSerializable(typeof(IReadOnlyList<RuleConfig>))]
public sealed partial class StrategyJson : JsonSerializerContext
{
    public static JsonSerializerOptions Indented => Default.Options;
}
