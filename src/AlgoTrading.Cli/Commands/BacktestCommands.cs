using System.CommandLine;
using System.Globalization;
using AlgoTrading.Application.Ports;
using AlgoTrading.Application.UseCases;
using AlgoTrading.Domain.Backtesting;
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
        var rules = new Option<FileInfo?>("--rules") { Description = "Catalogue de règles, plages comprises ; par défaut, les douze indicateurs directionnels." };
        var minK = new Option<int>("--min-k") { Description = "Nombre minimal de règles par combinaison.", DefaultValueFactory = _ => 2 };
        var maxK = new Option<int>("--max-k") { Description = "Nombre maximal de règles par combinaison.", DefaultValueFactory = _ => 4 };
        var top = new Option<int>("--top") { Description = "Nombre de combinaisons à afficher.", DefaultValueFactory = _ => 20 };
        var minTrades = new Option<int>("--min-trades") { Description = "Nombre minimal de trades pour être classée.", DefaultValueFactory = _ => 20 };
        var sample = new Option<long?>("--sample") { Description = "Tire au hasard ce nombre de combinaisons au lieu de toutes les essayer." };
        var seed = new Option<ulong>("--seed") { Description = "Graine du tirage aléatoire.", DefaultValueFactory = _ => 1UL };
        var allowVariants = new Option<bool>("--allow-same-indicator") { Description = "Autorise deux variantes d'un même indicateur dans une combinaison." };
        var parallel = new Option<bool>("--parallel") { Description = "Répartit l'exploration sur tous les cœurs.", DefaultValueFactory = _ => true };
        var from = new Option<DateOnly?>("--from") { Description = "Première séance." };
        var to = new Option<DateOnly?>("--to") { Description = "Dernière séance." };
        var cash = new Option<decimal>("--cash") { Description = "Capital de départ.", DefaultValueFactory = _ => 100_000m };
        var save = new Option<bool>("--save") { Description = "Persiste les combinaisons retenues." };

        var command = new Command("optimize", "Explore les combinaisons de règles et les classe.");
        foreach (var option in new Option[] { rules, minK, maxK, top, minTrades, sample, seed, allowVariants, parallel, from, to, cash, save })
        {
            command.Add(option);
        }

        command.SetAction(async (parse, cancellationToken) =>
        {
            var output = services.GetRequiredService<IConsoleWriter>();

            IReadOnlyList<RuleConfig> catalog = RuleRegistry.DefaultCatalog();

            if (parse.GetValue(rules) is { } file)
            {
                if (!file.Exists)
                {
                    output.WriteWarning($"Catalogue introuvable : {file.FullName}");
                    return 1;
                }

                var expanded = RuleCatalog.Parse(await File.ReadAllTextAsync(file.FullName, cancellationToken).ConfigureAwait(false));
                catalog = expanded.Rules;

                output.WriteLine(expanded.Discarded == 0
                    ? $"Catalogue : {expanded.Rules.Count} règles."
                    : $"Catalogue : {expanded.Rules.Count} règles, {expanded.Discarded} variantes invalides écartées.");
            }

            var response = await services.GetRequiredService<OptimizeStrategyHandler>().HandleAsync(
                new OptimizeStrategyRequest
                {
                    Catalog = catalog,
                    MinimumRules = parse.GetValue(minK),
                    MaximumRules = parse.GetValue(maxK),
                    Top = parse.GetValue(top),
                    MinimumTrades = parse.GetValue(minTrades),
                    SampleSize = parse.GetValue(sample),
                    Seed = parse.GetValue(seed),
                    OneVariantPerIndicator = !parse.GetValue(allowVariants),
                    From = parse.GetValue(from),
                    To = parse.GetValue(to),
                    InitialCash = parse.GetValue(cash),
                    Parallel = parse.GetValue(parallel),
                    Save = parse.GetValue(save),
                },
                new ConsoleProgress(output),
                cancellationToken).ConfigureAwait(false);

            var report = response.Report;
            output.WriteLine();

            if (report.Evaluated == 0)
            {
                output.WriteLine("Aucune combinaison à évaluer avec ces bornes.");
                return 0;
            }

            output.WriteLine(report.Evaluated < report.SearchSpace
                ? $"{report.Evaluated:N0} combinaisons tirées sur {report.SearchSpace:N0} possibles, {report.Eligible:N0} avec au moins {parse.GetValue(minTrades)} trades."
                : $"{report.Evaluated:N0} combinaisons essayées, {report.Eligible:N0} avec au moins {parse.GetValue(minTrades)} trades.");

            if (report.Top.Count == 0)
            {
                output.WriteLine("Aucune n'atteint le nombre minimal de trades.");
                return 0;
            }

            output.WriteLine("Classement par Calmar — rendement annualisé rapporté à la pire baisse.");
            output.WriteLine();

            output.WriteTable(
                ["Rang", "Combinaison", "Calmar", "Sharpe", "Rendement", "Pire baisse", "Trades"],
                [
                    .. report.Top.Select((c, i) => new[]
                    {
                        (i + 1).ToString(CultureInfo.CurrentCulture),
                        c.Strategy.Name,
                        c.Score.ToString("0.00", CultureInfo.CurrentCulture),
                        c.Metrics.Sharpe.ToString("0.00", CultureInfo.CurrentCulture),
                        ConsoleWriter.Percent(c.Metrics.TotalReturn),
                        ConsoleWriter.Percent(c.Metrics.MaxDrawdown),
                        c.Metrics.TradeCount.ToString(CultureInfo.CurrentCulture),
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

    /// <summary>
    /// Progression réécrite sur une seule ligne. Les rapports arrivent des fils de l'exploration
    /// parallèle, d'où le verrou ; <see cref="Progress{T}"/> ne convient pas, il les livrerait
    /// dans le désordre.
    /// </summary>
    private sealed class ConsoleProgress(IConsoleWriter output) : IProgress<OptimizationProgress>
    {
        private readonly Lock _gate = new();
        private long _shown;

        public void Report(OptimizationProgress value)
        {
            lock (_gate)
            {
                if (value.Evaluated <= _shown)
                {
                    return;
                }

                _shown = value.Evaluated;
                var best = value.Best is { } candidate
                    ? $" — meilleur Calmar {candidate.Score.ToString("0.00", CultureInfo.CurrentCulture)}"
                    : string.Empty;

                output.Write($"\r{value.Evaluated:N0} / {value.Planned:N0} ({(double)value.Evaluated / value.Planned:P0}){best}   ");
            }
        }
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
