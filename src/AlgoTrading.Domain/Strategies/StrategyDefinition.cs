using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AlgoTrading.Domain.Strategies;

/// <summary>
/// Une stratégie complète, sérialisable : ses règles d'entrée et de sortie, son
/// dimensionnement, sa gestion du risque et sa mécanique d'exécution.
/// <para><see cref="Fingerprint"/> résume tout cela en une empreinte stable : deux
/// exécutions de la même stratégie portent le même identifiant de run, et un résultat
/// persisté reste rattachable à ce qui l'a produit.</para>
/// </summary>
public sealed record StrategyDefinition
{
    private string? _fingerprint;

    public required string Name { get; init; }

    public required SignalPolicy Entry { get; init; }

    /// <summary>Absente, la sortie se déclenche sur le signal inverse de l'entrée.</summary>
    public SignalPolicy? Exit { get; init; }

    public PositionSizing Sizing { get; init; } = new();

    public RiskPolicy Risk { get; init; } = RiskPolicy.None;

    public ExecutionPolicy Execution { get; init; } = new();

    /// <summary>Empreinte SHA-256 de la forme canonique. Identique ⇒ même backtest.</summary>
    public string Fingerprint => _fingerprint ??= Hash(Canonical());

    public string ToJson() => JsonSerializer.Serialize(this, StrategyJson.Indented);

    public static StrategyDefinition FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var strategy = JsonSerializer.Deserialize(json, StrategyJson.Default.StrategyDefinition)
            ?? throw new ArgumentException("Le document ne décrit aucune stratégie.", nameof(json));

        strategy.Validate();
        return strategy;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Une stratégie doit avoir un nom.");
        }

        if (Entry.Rules.Count == 0)
        {
            throw new InvalidOperationException($"La stratégie « {Name} » n'a aucune règle d'entrée.");
        }

        // Construire les agrégateurs valide règles, indicateurs, seuils et poids d'un coup.
        _ = Entry.ToAggregator();
        _ = Exit?.ToAggregator();

        if (Sizing.Value <= 0m)
        {
            throw new InvalidOperationException($"Le dimensionnement de « {Name} » doit être strictement positif.");
        }

        if (Sizing.Mode == SizingMode.EquityFraction && Sizing.Value > 1m)
        {
            throw new InvalidOperationException($"Une fraction d'actif ne peut pas dépasser 1 ; « {Name} » demande {Sizing.Value}.");
        }

        if (Risk.StopLoss is <= 0m or > 1m)
        {
            throw new InvalidOperationException("Un stop de protection est une fraction strictement comprise entre 0 et 1.");
        }

        if (Risk.TakeProfit is <= 0m)
        {
            throw new InvalidOperationException("Une prise de bénéfice est une fraction strictement positive.");
        }
    }

    /// <summary>
    /// Forme canonique : un texte dont l'ordre ne dépend ni de l'ordre d'écriture du JSON,
    /// ni de celui d'énumération d'un dictionnaire.
    /// </summary>
    private string Canonical()
    {
        var builder = new StringBuilder();
        builder.Append("name=").Append(Name).Append('\n');

        Append(builder, "entry", Entry);
        if (Exit is { } exit)
        {
            Append(builder, "exit", exit);
        }

        builder.Append(CultureInfo.InvariantCulture, $"sizing={Sizing.Mode}:{Sizing.Value}\n");
        builder.Append(CultureInfo.InvariantCulture, $"risk={Risk.StopLoss?.ToString(CultureInfo.InvariantCulture) ?? "-"}:{Risk.TakeProfit?.ToString(CultureInfo.InvariantCulture) ?? "-"}\n");
        builder.Append(CultureInfo.InvariantCulture,
            $"execution={Execution.CommissionRate}:{Execution.MinimumCommission}:{Execution.SlippageRate}:{Execution.OrderValidityDays}:{Execution.ExitMode}:{Execution.ExitFraction}\n");

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string label, SignalPolicy policy)
    {
        builder.Append(CultureInfo.InvariantCulture, $"{label}.mode={policy.Mode}\n");
        builder.Append(CultureInfo.InvariantCulture, $"{label}.threshold={policy.Threshold}\n");

        foreach (var rule in policy.Rules
            .Select(static r => (Config: r, Rule: RuleRegistry.Create(r)))
            .OrderBy(static r => r.Rule.Name, StringComparer.Ordinal)
            .ThenBy(static r => r.Config.Weight))
        {
            builder.Append(CultureInfo.InvariantCulture,
                $"{label}.rule={rule.Config.Type}|{rule.Rule.Indicator.Id}|{rule.Rule.Kind}|{rule.Rule.Name}|w={rule.Config.Weight}\n");
        }
    }

    private static string Hash(string canonical) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
}
