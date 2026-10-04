using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Web.Forms;

/// <summary>
/// Les catalogues de règles du dossier <c>rules</c> de la ligne de commande — ceux que lit
/// <c>algo backtest optimize --rules</c> — plus le catalogue par défaut.
/// </summary>
public sealed class RuleCatalogLibrary(string directory)
{
    /// <summary>Nom réservé au catalogue intégré : les douze indicateurs à leurs réglages d'origine.</summary>
    public const string Default = "";

    /// <summary>Noms des catalogues, sans extension, triés.</summary>
    public IReadOnlyList<string> List() =>
        Directory.Exists(directory)
            ? [.. Directory.EnumerateFiles(directory, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)]
            : [];

    /// <summary>Le catalogue développé. Seul un nom listé est lu : rien hors du dossier.</summary>
    public RuleCatalog Read(string name)
    {
        if (name == Default)
        {
            return new RuleCatalog(RuleRegistry.DefaultCatalog(), 0);
        }

        if (!List().Contains(name, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Catalogue inconnu : « {name} ».", nameof(name));
        }

        return RuleCatalog.Parse(File.ReadAllText(Path.Combine(directory, name + ".json")));
    }
}
