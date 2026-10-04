using System.Text;
using System.Text.RegularExpressions;
using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Strategies;
using Microsoft.Extensions.Options;

namespace AlgoTrading.Infrastructure.Strategies;

public sealed class StrategyStoreOptions
{
    public const string Section = "Strategies";

    /// <summary>Répertoire des fichiers <c>*.json</c> — le même que celui que lit la ligne de commande.</summary>
    public string Directory { get; set; } = "strategies";
}

/// <summary>
/// Une stratégie par fichier JSON, la clé étant le nom du fichier sans extension. Les
/// fichiers restent ceux qu'on passe à <c>algo backtest run --strategy</c> : l'interface et
/// la ligne de commande travaillent sur la même bibliothèque.
/// </summary>
public sealed partial class JsonFileStrategyRepository(IOptions<StrategyStoreOptions> options) : IStrategyRepository
{
    private string Root => Path.GetFullPath(options.Value.Directory);

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> keys = Directory.Exists(Root)
            ? [.. Directory.EnumerateFiles(Root, "*.json")
                .Select(static f => Path.GetFileNameWithoutExtension(f))
                .Order(StringComparer.OrdinalIgnoreCase)]
            : [];

        return Task.FromResult(keys);
    }

    public async Task<string?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathOf(key);
        return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : null;
    }

    public async Task<string> SaveAsync(string key, StrategyDefinition strategy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        // Rien d'invalide n'atterrit sur disque : la ligne de commande refuserait de le relire.
        strategy.Validate();

        Directory.CreateDirectory(Root);
        await File.WriteAllTextAsync(PathOf(key), strategy.ToJson() + "\n", new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);

        return key;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        File.Delete(PathOf(key));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clé de fichier tirée d'un nom libre : « Bollinger et RSI » donne <c>bollinger-et-rsi</c>.
    /// </summary>
    public static string KeyFor(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var decomposed = name.Normalize(NormalizationForm.FormD);
        var ascii = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                ascii.Append(char.ToLowerInvariant(c));
            }
        }

        var key = Separators().Replace(ascii.ToString(), "-").Trim('-');
        return key.Length == 0 ? "strategie" : key;
    }

    /// <summary>La clé désigne un fichier du répertoire, jamais un chemin : pas de <c>../</c> possible.</summary>
    private string PathOf(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !ValidKey().IsMatch(key))
        {
            throw new ArgumentException($"Clé de stratégie invalide : « {key} ».", nameof(key));
        }

        return Path.Combine(Root, key + ".json");
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Separators();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]*$")]
    private static partial Regex ValidKey();
}
