using System.CommandLine;
using System.Globalization;
using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Backtesting;
using Microsoft.Extensions.DependencyInjection;

namespace AlgoTrading.Cli.Commands;

public static class ExplorationCommands
{
    public static Command Build(IServiceProvider services)
    {
        var explorations = new Command("explorations", "Journal des explorations, dont les essais relèvent la barre du Sharpe dégonflé.");

        explorations.Add(List(services));

        return explorations;
    }

    private static Command List(IServiceProvider services)
    {
        var limit = new Option<int>("--limit") { Description = "Nombre d'explorations affichées.", DefaultValueFactory = _ => 20 };

        var command = new Command("list", "Les dernières explorations menées jusqu'au bout.");
        command.Add(limit);

        command.SetAction(async (parse, cancellationToken) =>
        {
            var output = services.GetRequiredService<IConsoleWriter>();
            var entries = await services.GetRequiredService<IExplorationJournal>()
                .ListAsync(Math.Max(1, parse.GetValue(limit)), cancellationToken)
                .ConfigureAwait(false);

            if (entries.Count == 0)
            {
                output.WriteLine("Aucune exploration enregistrée.");
                return 0;
            }

            output.WriteTable(
                ["N°", "Date", "Type", "Titres", "Séances jugées", "Catalogue", "Objectif", "Essais", "Gagnant", "Sharpe dégonflé"],
                [
                    .. entries.Select(e => new[]
                    {
                        e.Id.ToString(CultureInfo.CurrentCulture),
                        e.RanAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
                        Kinds[e.Kind],
                        e.Universe.Count.ToString(CultureInfo.CurrentCulture),
                        $"{e.From:yyyy-MM-dd} → {e.To:yyyy-MM-dd}",
                        e.Catalog ?? "—",
                        Ranking.Name(e.Objective),
                        e.Trials.Trials.ToString("N0", CultureInfo.CurrentCulture),
                        e.Best ?? "—",
                        e.BestDeflatedSharpe is { } deflated ? ConsoleWriter.Percent(deflated) : "—",
                    }),
                ]);

            output.WriteLine();
            output.WriteLine("Une nouvelle exploration compte les essais de celles menées sur les mêmes titres dont les séances recoupent les siennes.");
            output.WriteLine("Une exploration interrompue n'est pas enregistrée.");

            return 0;
        });

        return command;
    }

    private static readonly Dictionary<ExplorationKind, string> Kinds = new()
    {
        [ExplorationKind.Screening] = "criblage",
        [ExplorationKind.Optimization] = "optimisation",
        [ExplorationKind.WalkForwardWindow] = "fenêtre de walk-forward",
    };
}
