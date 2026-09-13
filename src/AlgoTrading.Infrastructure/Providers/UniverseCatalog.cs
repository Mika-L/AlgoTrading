using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.MarketData;
using Microsoft.Extensions.Options;

namespace AlgoTrading.Infrastructure.Providers;

public sealed class UniverseOptions
{
    public const string Section = "Universes";

    /// <summary>Univers nommés : un nom, une liste de <c>TICKER=Raison sociale</c>.</summary>
    public Dictionary<string, List<string>> Definitions { get; set; } = [];

    /// <summary>
    /// Tickers écartés de tout univers et de tout backtest.
    /// <para>C'est le remplaçant honnête du <c>if (symbol == "ATO.PA" &amp;&amp; price &gt; 100) return;</c>
    /// et du <c>Where(p =&gt; p.Id != 6)</c> d'origine : une exclusion se déclare, se date et
    /// se justifie en configuration, elle ne se cache pas au milieu d'une boucle.</para>
    /// </summary>
    public List<string> Exclusions { get; set; } = [];
}

/// <summary>
/// Résout un univers nommé. La composition vit en configuration : c'est ce qui permet de
/// sortir un titre du périmètre sans toucher au code — le <c>Where(p =&gt; p.Id != 6)</c> et
/// le <c>if (symbol == "ATO.PA" &amp;&amp; price &gt; 100) return;</c> d'origine n'ont plus lieu d'être.
/// </summary>
public sealed class UniverseCatalog(IOptions<UniverseOptions> options) : IUniverseCatalog
{
    public IReadOnlyList<string> Names => [.. options.Value.Definitions.Keys.Order(StringComparer.OrdinalIgnoreCase)];

    private HashSet<string> Excluded => [.. options.Value.Exclusions.Select(static e => e.Trim().ToUpperInvariant())];

    public IReadOnlyList<(Symbol Symbol, string Name)> Resolve(string universeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(universeName);

        var definitions = options.Value.Definitions;

        var entry = definitions.FirstOrDefault(d => string.Equals(d.Key, universeName, StringComparison.OrdinalIgnoreCase));

        if (entry.Value is null)
        {
            // Un univers inconnu peut aussi être une liste de tickers passée à la volée.
            var inline = universeName
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static t => t.Contains('.', StringComparison.Ordinal) || t.All(char.IsLetterOrDigit))
                .ToArray();

            if (inline.Length > 0 && definitions.Count > 0)
            {
                return [.. inline.Select(static t => (Symbol.From(t), t)).Where(e => !Excluded.Contains(e.Item1.Ticker))];
            }

            throw new ArgumentException(
                $"Univers inconnu : « {universeName} ». Connus : {(Names.Count == 0 ? "aucun" : string.Join(", ", Names))}.",
                nameof(universeName));
        }

        return [.. entry.Value.Select(Parse).Where(e => !Excluded.Contains(e.Symbol.Ticker))];
    }

    /// <summary>Une entrée vaut <c>TICKER=Raison sociale</c>, ou simplement <c>TICKER</c>.</summary>
    private static (Symbol Symbol, string Name) Parse(string definition)
    {
        var separator = definition.IndexOf('=', StringComparison.Ordinal);

        if (separator < 0)
        {
            var ticker = definition.Trim();
            return (Symbol.From(ticker), ticker);
        }

        var symbol = definition[..separator].Trim();
        var name = definition[(separator + 1)..].Trim();

        return (Symbol.From(symbol), string.IsNullOrWhiteSpace(name) ? symbol : name);
    }
}
