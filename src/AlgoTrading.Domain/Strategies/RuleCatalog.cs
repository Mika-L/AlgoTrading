using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AlgoTrading.Domain.Strategies;

/// <summary>Catalogue développé : les règles concrètes, et ce qui a été écarté en chemin.</summary>
/// <param name="Rules">Variantes valides, sans doublon, dans l'ordre du fichier.</param>
/// <param name="Discarded">Variantes rejetées par la validation d'une règle — un seuil de survente
/// au-dessus du seuil de surachat, une période courte plus longue que la longue.</param>
public sealed record RuleCatalog(IReadOnlyList<RuleConfig> Rules, int Discarded)
{
    /// <summary>Garde-fou contre une faute de frappe : <c>{ "from": 1, "to": 1000, "step": 0.01 }</c>.</summary>
    public const int MaximumValuesPerAxis = 500;

    /// <summary>
    /// Lit un catalogue de règles candidates. Toute valeur d'une règle — paramètre d'indicateur
    /// compris — peut y être remplacée par une liste, <c>"period": [7, 14, 21]</c>, ou par une
    /// plage, <c>"period": { "from": 5, "to": 30, "step": 5 }</c>. Chaque entrée est développée
    /// en autant de règles que le produit de ses axes.
    /// <para>Une variante invalide est écartée et comptée, pas fatale : sur une grille de seuils,
    /// <c>bullishBelow = 40</c> et <c>bearishAbove = 35</c> se croisent forcément. Une entrée dont
    /// <b>aucune</b> variante n'est valide reste une erreur — c'est une faute, pas une grille.</para>
    /// </summary>
    public static RuleCatalog Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        if (JsonNode.Parse(json) is not JsonArray entries)
        {
            throw new ArgumentException("Un catalogue de règles est un tableau JSON.", nameof(json));
        }

        if (entries.Count == 0)
        {
            throw new ArgumentException("Le catalogue de règles est vide.", nameof(json));
        }

        var rules = new List<RuleConfig>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var discarded = 0;

        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index] is not JsonObject entry)
            {
                throw new ArgumentException($"L'entrée {index + 1} du catalogue n'est pas un objet.", nameof(json));
            }

            var valid = 0;
            ArgumentException? firstError = null;

            foreach (var variant in Expand(entry, index))
            {
                var rule = variant.Deserialize(StrategyJson.Default.RuleConfig)
                    ?? throw new ArgumentException($"L'entrée {index + 1} du catalogue ne décrit aucune règle.", nameof(json));

                try
                {
                    // Construire la règle valide seuils, poids et paramètres d'indicateur au plus tôt.
                    _ = RuleRegistry.Create(rule);
                }
                catch (ArgumentException error)
                {
                    firstError ??= error;
                    discarded++;
                    continue;
                }

                valid++;

                if (seen.Add(JsonSerializer.Serialize(rule, StrategyJson.Default.RuleConfig)))
                {
                    rules.Add(rule);
                }
            }

            if (valid == 0)
            {
                throw new ArgumentException($"Aucune variante valide pour l'entrée {index + 1} du catalogue : {firstError?.Message}", nameof(json), firstError);
            }
        }

        return new RuleCatalog(rules, discarded);
    }

    /// <summary>Le produit cartésien des axes d'une entrée, chaque combinaison en objet JSON concret.</summary>
    private static IEnumerable<JsonObject> Expand(JsonObject entry, int index)
    {
        var axes = new List<(JsonObject Owner, string Name, JsonNode?[] Values)>();

        // Les paramètres d'abord : les variantes d'un même indicateur se suivent, seuils à l'intérieur.
        if (entry["parameters"] is JsonObject parameters)
        {
            CollectAxes(parameters, axes, index);
        }

        CollectAxes(entry, axes, index);

        var choice = new int[axes.Count];

        while (true)
        {
            for (var axis = 0; axis < axes.Count; axis++)
            {
                var (owner, name, values) = axes[axis];
                owner[name] = values[choice[axis]]?.DeepClone();
            }

            yield return (JsonObject)entry.DeepClone();

            // Incrément d'un compteur à bases mixtes : le dernier axe tourne le plus vite.
            var position = axes.Count - 1;
            while (position >= 0 && ++choice[position] == axes[position].Values.Length)
            {
                choice[position] = 0;
                position--;
            }

            if (position < 0)
            {
                yield break;
            }
        }
    }

    private static void CollectAxes(JsonObject owner, List<(JsonObject Owner, string Name, JsonNode?[] Values)> axes, int index)
    {
        foreach (var (name, value) in owner.ToArray())
        {
            var values = value switch
            {
                JsonArray list => [.. list],
                JsonObject range when range.ContainsKey("from") || range.ContainsKey("to") => Range(range, name, index),
                _ => null,
            };

            if (values is null)
            {
                continue;
            }

            if (values.Length == 0)
            {
                throw new ArgumentException($"La liste « {name} » de l'entrée {index + 1} est vide.", nameof(owner));
            }

            axes.Add((owner, name, values));
        }
    }

    private static JsonNode?[] Range(JsonObject range, string name, int index)
    {
        decimal Read(string key, decimal? fallback) => range[key] switch
        {
            JsonValue value when value.TryGetValue<decimal>(out var number) => number,
            null when fallback is { } defaulted => defaulted,
            _ => throw new ArgumentException($"La plage « {name} » de l'entrée {index + 1} exige un nombre « {key} ».", nameof(range)),
        };

        var from = Read("from", null);
        var to = Read("to", null);
        var step = Read("step", 1m);

        if (step <= 0m)
        {
            throw new ArgumentException($"Le pas de la plage « {name} » de l'entrée {index + 1} doit être strictement positif.", nameof(range));
        }

        if (to < from)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"La plage « {name} » de l'entrée {index + 1} va de {from} à {to} : la borne haute précède la basse."),
                nameof(range));
        }

        var values = new List<JsonNode?>();

        for (var i = 0; from + (i * step) <= to; i++)
        {
            if (values.Count == MaximumValuesPerAxis)
            {
                throw new ArgumentException($"La plage « {name} » de l'entrée {index + 1} dépasse {MaximumValuesPerAxis} valeurs.", nameof(range));
            }

            values.Add(JsonValue.Create(from + (i * step)));
        }

        return [.. values];
    }
}
