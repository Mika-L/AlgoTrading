using System.CommandLine;
using System.Globalization;
using AlgoTrading.Application.Ports;
using AlgoTrading.Application.UseCases;
using AlgoTrading.Domain.Reporting;
using AlgoTrading.Domain.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace AlgoTrading.Cli.Commands;

public static class BacktestCommands
{
    public static Command Build(IServiceProvider services)
    {
        var backtest = new Command("backtest", "Exécution et comparaison de backtests.");

        backtest.Add(Run(services));
        backtest.Add(Optimize(services));
        backtest.Add(Compare(services));

        return backtest;
    }

    private static Command Run(IServiceProvider services)
    {
        var strategy = new Option<FileInfo>("--strategy") { Description = "Fichier JSON décrivant la stratégie.", Required = true };
        var from = new Option<DateOnly?>("--from") { Description = "Première séance du backtest." };
        var to = new Option<DateOnly?>("--to") { Description = "Dernière séance du backtest." };
        var cash = new Option<decimal>("--cash") { Description = "Capital de départ.", DefaultValueFactory = _ => 100_000m };
        var save = new Option<bool>("--save") { Description = "Persiste le résultat pour pouvoir le comparer." };
        var frictionless = new Option<bool>("--frictionless") { Description = "Neutralise commissions et glissement (run de comparaison)." };

        var command = new Command("run", "Exécute une stratégie sur l'historique.");
        foreach (var option in new Option[] { strategy, from, to, cash, save, frictionless })
        {
            command.Add(option);
        }

        command.SetAction(async (parse, cancellationToken) =>
        {
            var output = services.GetRequiredService<IConsoleWriter>();
            var file = parse.GetValue(strategy)!;

            if (!file.Exists)
            {
                output.WriteWarning($"Stratégie introuvable : {file.FullName}");
                return 1;
            }

            var definition = StrategyDefinition.FromJson(await File.ReadAllTextAsync(file.FullName, cancellationToken).ConfigureAwait(false));

            var response = await services.GetRequiredService<RunBacktestHandler>().HandleAsync(new RunBacktestRequest
            {
                Strategy = definition,
                From = parse.GetValue(from),
                To = parse.GetValue(to),
                InitialCash = parse.GetValue(cash),
                Save = parse.GetValue(save),
                Frictionless = parse.GetValue(frictionless),
            }, cancellationToken).ConfigureAwait(false);

            ReportCommands.Print(output, response.Result);

            if (response.RunId is { } id)
            {
                output.WriteLine();
                output.WriteLine($"Résultat enregistré sous le numéro {id}.");
            }

            return 0;
        });

        return command;
    }

    private static Command Optimize(IServiceProvider services)
    {
        var rules = new Option<FileInfo?>("--rules") { Description = "Catalogue de règles ; par défaut, les douze indicateurs directionnels." };
        var minK = new Option<int>("--min-k") { Description = "Nombre minimal de règles par combinaison.", DefaultValueFactory = _ => 2 };
        var maxK = new Option<int>("--max-k") { Description = "Nombre maximal de règles par combinaison.", DefaultValueFactory = _ => 4 };
        var top = new Option<int>("--top") { Description = "Nombre de combinaisons à afficher.", DefaultValueFactory = _ => 20 };
        var parallel = new Option<bool>("--parallel") { Description = "Répartit l'exploration sur tous les cœurs.", DefaultValueFactory = _ => true };
        var from = new Option<DateOnly?>("--from") { Description = "Première séance." };
        var to = new Option<DateOnly?>("--to") { Description = "Dernière séance." };
        var cash = new Option<decimal>("--cash") { Description = "Capital de départ.", DefaultValueFactory = _ => 100_000m };
        var save = new Option<bool>("--save") { Description = "Persiste les combinaisons retenues." };

        var command = new Command("optimize", "Explore les combinaisons de règles et les classe.");
        foreach (var option in new Option[] { rules, minK, maxK, top, parallel, from, to, cash, save })
        {
            command.Add(option);
        }

        command.SetAction(async (parse, cancellationToken) =>
        {
            var output = services.GetRequiredService<IConsoleWriter>();

            var catalog = parse.GetValue(rules) is { Exists: true } file
                ? RuleCatalogFile.Read(await File.ReadAllTextAsync(file.FullName, cancellationToken).ConfigureAwait(false))
                : RuleRegistry.DefaultCatalog();

            var response = await services.GetRequiredService<OptimizeStrategyHandler>().HandleAsync(new OptimizeStrategyRequest
            {
                Catalog = catalog,
                MinimumRules = parse.GetValue(minK),
                MaximumRules = parse.GetValue(maxK),
                Top = parse.GetValue(top),
                From = parse.GetValue(from),
                To = parse.GetValue(to),
                InitialCash = parse.GetValue(cash),
                Parallel = parse.GetValue(parallel),
                Save = parse.GetValue(save),
            }, cancellationToken).ConfigureAwait(false);

            if (response.Outcomes.Count == 0)
            {
                output.WriteLine("Aucune combinaison à évaluer avec ces bornes.");
                return 0;
            }

            output.WriteLine("Classement par Calmar — rendement annualisé rapporté à la pire baisse.");
            output.WriteLine();

            output.WriteTable(
                ["Rang", "Combinaison", "Calmar", "Rendement", "Pire baisse", "Trades"],
                [
                    .. response.Outcomes.Select((o, i) => new[]
                    {
                        (i + 1).ToString(CultureInfo.CurrentCulture),
                        o.Strategy.Name,
                        o.Score.ToString("0.00", CultureInfo.CurrentCulture),
                        ConsoleWriter.Percent(o.Result.Metrics.TotalReturn),
                        ConsoleWriter.Percent(o.Result.Metrics.MaxDrawdown),
                        o.Result.Metrics.TradeCount.ToString(CultureInfo.CurrentCulture),
                    }),
                ]);

            if (response.SavedRunIds.Count > 0)
            {
                output.WriteLine();
                output.WriteLine($"Résultats enregistrés sous les numéros {string.Join(", ", response.SavedRunIds)}.");
            }

            return 0;
        });

        return command;
    }

    private static Command Compare(IServiceProvider services)
    {
        var runs = new Option<string>("--runs") { Description = "Numéros de runs à comparer, séparés par des virgules.", Required = true };

        var command = new Command("compare", "Compare des résultats déjà enregistrés.");
        command.Add(runs);

        command.SetAction(async (parse, cancellationToken) =>
        {
            var store = services.GetRequiredService<IBacktestRunStore>();
            var output = services.GetRequiredService<IConsoleWriter>();

            var ids = parse.GetValue(runs)!
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static token => int.TryParse(token, CultureInfo.InvariantCulture, out var id) ? id : -1)
                .Where(static id => id > 0)
                .ToArray();

            if (ids.Length < 2)
            {
                output.WriteWarning("Indiquez au moins deux numéros de runs, par exemple « --runs 12,17 ».");
                return 1;
            }

            var rows = new List<IReadOnlyList<string>>();

            foreach (var id in ids)
            {
                var summary = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);

                if (summary is null)
                {
                    output.WriteWarning($"Run {id} introuvable.");
                    continue;
                }

                rows.Add(
                [
                    summary.Id.ToString(CultureInfo.CurrentCulture),
                    summary.StrategyName,
                    ConsoleWriter.Money(summary.FinalEquity),
                    ConsoleWriter.Percent(summary.TotalReturn),
                    ConsoleWriter.Percent(summary.MaxDrawdown),
                    summary.Calmar.ToString("0.00", CultureInfo.CurrentCulture),
                    summary.TradeCount.ToString(CultureInfo.CurrentCulture),
                ]);
            }

            if (rows.Count == 0)
            {
                return 1;
            }

            output.WriteTable(["Run", "Stratégie", "Valeur finale", "Rendement", "Pire baisse", "Calmar", "Trades"], rows);

            return 0;
        });

        return command;
    }
}
