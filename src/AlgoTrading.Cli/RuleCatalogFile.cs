using System.Text.Json;
using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Cli;

/// <summary>Lit un catalogue de règles candidates, tel que consommé par l'optimiseur.</summary>
public static class RuleCatalogFile
{
    public static IReadOnlyList<RuleConfig> Read(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var rules = JsonSerializer.Deserialize(json, StrategyJson.Default.IReadOnlyListRuleConfig)
            ?? throw new ArgumentException("Le document ne décrit aucune règle.", nameof(json));

        if (rules.Count == 0)
        {
            throw new ArgumentException("Le catalogue de règles est vide.", nameof(json));
        }

        // Construire chaque règle valide seuils, poids et paramètres d'indicateur au plus tôt.
        foreach (var rule in rules)
        {
            _ = RuleRegistry.Create(rule);
        }

        return rules;
    }
}
