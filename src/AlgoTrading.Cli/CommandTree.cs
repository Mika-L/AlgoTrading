using System.CommandLine;
using AlgoTrading.Cli.Commands;

namespace AlgoTrading.Cli;

public static class CommandTree
{
    public static RootCommand Build(IServiceProvider services)
    {
        var root = new RootCommand("Backtester de stratégies sur indicateurs techniques.");

        // Déclarées ici pour l'aide et la validation : leur valeur a déjà été lue au démarrage.
        foreach (var option in new Option[]
        {
            new Option<string?>("--config") { Description = "Fichier de configuration additionnel.", Recursive = true },
            new Option<string?>("--db") { Description = "Chemin de la base SQLite.", Recursive = true },
            new Option<string?>("--verbosity") { Description = "Niveau de journalisation : quiet, error, warning, info, debug, trace.", Recursive = true },
        })
        {
            root.Add(option);
        }

        root.Add(DbCommands.Build(services));
        root.Add(DataCommands.Build(services));
        root.Add(BacktestCommands.Build(services));
        root.Add(ReportCommands.Build(services));

        return root;
    }
}
