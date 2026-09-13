namespace AlgoTrading.Cli;

/// <summary>
/// Options lues <b>avant</b> la construction de l'hôte : elles décident de la configuration
/// elle-même, et ne peuvent donc pas attendre l'analyse complète de la ligne de commande.
/// <para>C'est aussi pourquoi l'hôte est construit sans <c>args</c> : le fournisseur de
/// configuration en ligne de commande de l'hôte et l'analyseur de la CLI se disputeraient
/// les mêmes jetons.</para>
/// </summary>
public sealed record BootstrapOptions
{
    public string? ConfigFile { get; init; }

    public string? DatabasePath { get; init; }

    public string Verbosity { get; init; } = "warning";

    public static BootstrapOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return new BootstrapOptions
        {
            ConfigFile = Read(args, "--config"),
            DatabasePath = Read(args, "--db"),
            Verbosity = Read(args, "--verbosity") ?? "warning",
        };
    }

    private static string? Read(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal) && i + 1 < args.Count)
            {
                return args[i + 1];
            }

            var prefix = name + "=";
            if (args[i].StartsWith(prefix, StringComparison.Ordinal))
            {
                return args[i][prefix.Length..];
            }
        }

        return null;
    }
}
