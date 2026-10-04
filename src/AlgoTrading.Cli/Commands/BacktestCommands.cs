using System.CommandLine;
using System.Globalization;
using System.Text.Json;
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
        backtest.Add(Screen(services));
        backtest.Add(Optimize(services));
        backtest.Add(WalkForward(services));
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
        var objective = ObjectiveOption();

        var command = new Command("optimize", "Explore les combinaisons de règles et les classe.");
        foreach (var option in new Option[] { rules, minK, maxK, top, minTrades, sample, seed, allowVariants, parallel, from, to, cash, save, objective })
        {
            command.Add(option);
        }

        command.SetAction(async (parse, cancellationToken) =>
        {
            var output = services.GetRequiredService<IConsoleWriter>();

            var catalog = await ReadCatalogAsync(parse.GetValue(rules), output, cancellationToken).ConfigureAwait(false);

            if (catalog is null)
            {
                return 1;
            }

            var ranking = parse.GetValue(objective);
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
                    Objective = ranking,
                },
                new ConsoleProgress(output, ranking),
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

            output.WriteLine(report.BenchmarkScore is { } bar
                ? $"Classement par {Ranking.Name(ranking)}. Acheter et conserver l'univers obtient {bar.ToString("0.00", CultureInfo.CurrentCulture)}."
                : $"Classement par {Ranking.Name(ranking)}.");
            output.WriteLine();

            var alongside = Alongside(ranking);
            output.WriteTable(
                ["Rang", "Combinaison", Ranking.Name(ranking), .. alongside.Select(static m => m.Name), "Rendement", "Pire baisse", "Trades"],
                [
                    .. report.Top.Select(string[] (c, i) =>
                    [
                        (i + 1).ToString(CultureInfo.CurrentCulture),
                        c.Strategy.Name,
                        c.Score.ToString("0.00", CultureInfo.CurrentCulture),
                        .. alongside.Select(m => m.Read(c.Metrics).ToString("0.00", CultureInfo.CurrentCulture)),
                        ConsoleWriter.Percent(c.Metrics.TotalReturn),
                        ConsoleWriter.Percent(c.Metrics.MaxDrawdown),
                        c.Metrics.TradeCount.ToString(CultureInfo.CurrentCulture),
                    ]),
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

    private static Command Screen(IServiceProvider services)
    {
        var rules = new Option<FileInfo?>("--rules") { Description = "Catalogue de règles, plages comprises ; par défaut, les douze indicateurs directionnels." };
        var perIndicator = new Option<int>("--top-per-indicator") { Description = "Variantes affichées, et exportées, par indicateur.", DefaultValueFactory = _ => 3 };
        var minTrades = new Option<int>("--min-trades") { Description = "En deçà, une variante compte pour zéro dans son voisinage.", DefaultValueFactory = _ => 20 };
        var export = new Option<FileInfo?>("--export") { Description = "Écrit en catalogue, prêt pour optimize ou walk-forward, les meilleures variantes éligibles de chaque indicateur dont le voisinage est positif." };
        var parallel = new Option<bool>("--parallel") { Description = "Répartit l'exploration sur tous les cœurs.", DefaultValueFactory = _ => true };
        var from = new Option<DateOnly?>("--from") { Description = "Première séance." };
        var to = new Option<DateOnly?>("--to") { Description = "Dernière séance." };
        var cash = new Option<decimal>("--cash") { Description = "Capital de départ.", DefaultValueFactory = _ => 100_000m };
        var objective = ObjectiveOption();

        var command = new Command("screen", "Joue chaque variante seule et la juge avec ses voisines de grille, indicateur par indicateur.");
        foreach (var option in new Option[] { rules, perIndicator, minTrades, export, parallel, from, to, cash, objective })
        {
            command.Add(option);
        }

        command.SetAction(async (parse, cancellationToken) =>
        {
            var output = services.GetRequiredService<IConsoleWriter>();
            // Vérifié avant le calcul, pas après : un chemin faux ne doit pas coûter un criblage.
            if (parse.GetValue(export) is { Directory.Exists: false } unreachable)
            {
                output.WriteWarning($"Dossier d'export introuvable : {unreachable.DirectoryName}");
                return 1;
            }

            var catalog = await ReadCatalogAsync(parse.GetValue(rules), output, cancellationToken).ConfigureAwait(false);

            if (catalog is null)
            {
                return 1;
            }

            var ranking = parse.GetValue(objective);
            var name = Ranking.Name(ranking);
            var report = await services.GetRequiredService<ScreenRulesHandler>().HandleAsync(
                new ScreenRulesRequest
                {
                    Catalog = catalog,
                    MinimumTrades = parse.GetValue(minTrades),
                    From = parse.GetValue(from),
                    To = parse.GetValue(to),
                    InitialCash = parse.GetValue(cash),
                    Parallel = parse.GetValue(parallel),
                    Objective = ranking,
                },
                new ConsoleProgress(output, ranking),
                cancellationToken).ConfigureAwait(false);

            var shown = Math.Max(1, parse.GetValue(perIndicator));
            var alongside = Alongside(ranking);

            output.WriteLine();
            output.WriteLine($"Voisinage : {name} moyen de la variante et de ses voisines (un réglage décalé d'un cran).");
            output.WriteLine($"Un plateau le garde proche du score ; un pic isolé l'en éloigne. Moins de {parse.GetValue(minTrades)} trades compte pour zéro.");

            foreach (var group in report.Groups)
            {
                output.WriteLine();
                output.WriteLine($"{group.Indicator} — {group.Variants.Count} variantes");
                output.WriteTable(
                    ["Variante", "Voisinage", name, "Pire voisine", "Voisines", .. alongside.Select(static m => m.Name), "Trades"],
                    [
                        .. group.Variants.Take(shown).Select(string[] (v) =>
                        [
                            v.Label,
                            v.NeighbourhoodScore.ToString("0.00", CultureInfo.CurrentCulture),
                            v.Score.ToString("0.00", CultureInfo.CurrentCulture) + (v.Eligible ? string.Empty : " *"),
                            v.WorstNeighbour?.ToString("0.00", CultureInfo.CurrentCulture) ?? "—",
                            v.Neighbours.ToString(CultureInfo.CurrentCulture),
                            .. alongside.Select(m => m.Read(v.Metrics).ToString("0.00", CultureInfo.CurrentCulture)),
                            v.Metrics.TradeCount.ToString(CultureInfo.CurrentCulture),
                        ]),
                    ]);
            }

            output.WriteLine();
            output.WriteLine($"{report.Evaluated:N0} variantes essayées. * : trop peu de trades pour être retenue.");

            if (parse.GetValue(export) is { } target)
            {
                var retained = report.Shortlist(shown);

                await File.WriteAllTextAsync(
                    target.FullName,
                    JsonSerializer.Serialize(retained, StrategyJson.Default.IReadOnlyListRuleConfig),
                    cancellationToken).ConfigureAwait(false);

                output.WriteLine($"{retained.Count} variantes exportées dans {target.FullName}.");
            }

            return 0;
        });

        return command;
    }

    private static Command WalkForward(IServiceProvider services)
    {
        var rules = new Option<FileInfo?>("--rules") { Description = "Catalogue de règles, plages comprises ; par défaut, les douze indicateurs directionnels." };
        var training = new Option<int>("--train-months") { Description = "Durée de chaque fenêtre d'apprentissage, en mois.", DefaultValueFactory = _ => 36 };
        var test = new Option<int>("--test-months") { Description = "Durée de chaque fenêtre de test, et décalage entre deux fenêtres.", DefaultValueFactory = _ => 12 };
        var minK = new Option<int>("--min-k") { Description = "Nombre minimal de règles par combinaison.", DefaultValueFactory = _ => 2 };
        var maxK = new Option<int>("--max-k") { Description = "Nombre maximal de règles par combinaison.", DefaultValueFactory = _ => 4 };
        var minTrades = new Option<int>("--min-trades") { Description = "Nombre minimal de trades en apprentissage pour être retenue.", DefaultValueFactory = _ => 20 };
        var sample = new Option<long?>("--sample") { Description = "Tire au hasard ce nombre de combinaisons par fenêtre au lieu de toutes les essayer." };
        var seed = new Option<ulong>("--seed") { Description = "Graine du tirage aléatoire.", DefaultValueFactory = _ => 1UL };
        var allowVariants = new Option<bool>("--allow-same-indicator") { Description = "Autorise deux variantes d'un même indicateur dans une combinaison." };
        var parallel = new Option<bool>("--parallel") { Description = "Répartit l'exploration sur tous les cœurs.", DefaultValueFactory = _ => true };
        var from = new Option<DateOnly?>("--from") { Description = "Début de la première fenêtre." };
        var to = new Option<DateOnly?>("--to") { Description = "Fin de la dernière fenêtre — s'arrêter avant la période réservée au verdict final." };
        var cash = new Option<decimal>("--cash") { Description = "Capital de départ.", DefaultValueFactory = _ => 100_000m };
        var objective = ObjectiveOption();
        var screenTop = new Option<int?>("--screen-top") { Description = "Crible le catalogue dans chaque fenêtre d'apprentissage et n'en combine que ce nombre de variantes par indicateur." };
        var requireEdge = new Option<bool>("--require-edge") { Description = "Reste en liquidités quand le gagnant d'apprentissage ne bat pas le marché sur l'objectif." };

        var command = new Command("walk-forward", "Optimise sur une fenêtre, joue le gagnant sur la suivante, et recommence.");
        foreach (var option in new Option[] { rules, training, test, minK, maxK, minTrades, sample, seed, allowVariants, parallel, from, to, cash, objective, screenTop, requireEdge })
        {
            command.Add(option);
        }

        command.SetAction(async (parse, cancellationToken) =>
        {
            var output = services.GetRequiredService<IConsoleWriter>();
            var catalog = await ReadCatalogAsync(parse.GetValue(rules), output, cancellationToken).ConfigureAwait(false);

            if (catalog is null)
            {
                return 1;
            }

            if (parse.GetValue(screenTop) is < 1)
            {
                output.WriteWarning("--screen-top doit valoir au moins 1.");
                return 1;
            }

            var ranking = parse.GetValue(objective);
            var name = Ranking.Name(ranking);
            var report = await services.GetRequiredService<RunWalkForwardHandler>().HandleAsync(
                new RunWalkForwardRequest
                {
                    Catalog = catalog,
                    TrainingMonths = parse.GetValue(training),
                    TestMonths = parse.GetValue(test),
                    MinimumRules = parse.GetValue(minK),
                    MaximumRules = parse.GetValue(maxK),
                    MinimumTrades = parse.GetValue(minTrades),
                    SampleSize = parse.GetValue(sample),
                    Seed = parse.GetValue(seed),
                    OneVariantPerIndicator = !parse.GetValue(allowVariants),
                    From = parse.GetValue(from),
                    To = parse.GetValue(to),
                    InitialCash = parse.GetValue(cash),
                    Parallel = parse.GetValue(parallel),
                    Objective = ranking,
                    ScreenTopPerIndicator = parse.GetValue(screenTop),
                    RequireEdge = parse.GetValue(requireEdge),
                },
                new ConsoleWalkForwardProgress(output),
                cancellationToken).ConfigureAwait(false);

            output.WriteLine();
            output.WriteLine("Chaque gagnant d'apprentissage n'est jugé que sur la fenêtre de test qui suit.");
            output.WriteLine();

            output.WriteTable(
                ["Apprentissage", "Test", "Règles", "Retenue", $"{name} appr.", "Marché appr.", "Rendement test", "Marché", "Calmar test", "Trades test"],
                [
                    .. report.Windows.Select(w => new[]
                    {
                        $"{w.TrainFrom:yyyy-MM-dd} → {w.TrainTo:yyyy-MM-dd}",
                        $"{w.TestFrom:yyyy-MM-dd} → {w.TestTo:yyyy-MM-dd}",
                        w.Catalog.Count.ToString(CultureInfo.CurrentCulture),
                        w.Test is null ? $"en liquidités — {w.CashReason}" : w.Selected!.Strategy.Name,
                        w.Selected?.Score.ToString("0.00", CultureInfo.CurrentCulture) ?? "—",
                        w.Training.BenchmarkScore?.ToString("0.00", CultureInfo.CurrentCulture) ?? "—",
                        w.Test is null ? "—" : ConsoleWriter.Percent(w.Test.Metrics.TotalReturn),
                        ConsoleWriter.Percent(w.BenchmarkReturn),
                        w.Test?.Metrics.Calmar.ToString("0.00", CultureInfo.CurrentCulture) ?? "—",
                        w.Test?.Metrics.TradeCount.ToString(CultureInfo.CurrentCulture) ?? "—",
                    }),
                ]);

            static string[] Row(string label, PerformanceMetrics metrics) =>
            [
                label,
                ConsoleWriter.Percent(metrics.TotalReturn),
                ConsoleWriter.Percent(metrics.AnnualisedReturn),
                ConsoleWriter.Percent(metrics.MaxDrawdown),
                metrics.Calmar.ToString("0.00", CultureInfo.CurrentCulture),
                metrics.Sharpe.ToString("0.00", CultureInfo.CurrentCulture),
            ];

            output.WriteLine();
            output.WriteLine("Hors échantillon, fenêtres de test mises bout à bout :");
            output.WriteTable(
                ["", "Rendement", "Annualisé", "Pire baisse", "Calmar", "Sharpe"],
                [Row("Stratégie", report.OutOfSample), Row("Acheter et conserver", report.Benchmark)]);

            output.WriteLine();
            output.WriteLine($"Fenêtres gagnantes : {ConsoleWriter.Percent(report.ProfitableWindowShare)}. Fenêtres au-dessus du marché : {ConsoleWriter.Percent(report.OutperformingWindowShare)}. Trades : {report.OutOfSample.TradeCount:N0}.");
            output.WriteLine($"Efficacité (rendement annualisé en test rapporté à l'apprentissage) : {report.Efficiency?.ToString("0.00", CultureInfo.CurrentCulture) ?? "indéfinie"}.");
            output.WriteLine($"{report.Evaluated:N0} stratégies essayées au total, variantes criblées comprises.");

            return 0;
        });

        return command;
    }

    /// <summary>Les mesures affichées à côté du score, sans répéter celle qui sert d'objectif.</summary>
    private static (string Name, Func<PerformanceMetrics, decimal> Read)[] Alongside(RankingObjective objective) =>
    [
        .. new (RankingObjective Objective, Func<PerformanceMetrics, decimal> Read)[]
        {
            (RankingObjective.Calmar, static m => m.Calmar),
            (RankingObjective.Sharpe, static m => m.Sharpe),
        }.Where(m => m.Objective != objective).Select(static m => (Ranking.Name(m.Objective), m.Read)),
    ];

    private static Option<RankingObjective> ObjectiveOption() => new("--objective")
    {
        Description = "Ce que le classement maximise : Calmar, Sharpe, ou InformationRatio, l'écart au marché rapporté à sa volatilité.",
        DefaultValueFactory = _ => RankingObjective.Calmar,
    };

    /// <summary>Le catalogue d'un fichier, plages développées, ou le catalogue par défaut ; rien si le fichier manque.</summary>
    private static async Task<IReadOnlyList<RuleConfig>?> ReadCatalogAsync(FileInfo? file, IConsoleWriter output, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return RuleRegistry.DefaultCatalog();
        }

        if (!file.Exists)
        {
            output.WriteWarning($"Catalogue introuvable : {file.FullName}");
            return null;
        }

        var expanded = RuleCatalog.Parse(await File.ReadAllTextAsync(file.FullName, cancellationToken).ConfigureAwait(false));

        output.WriteLine(expanded.Discarded == 0
            ? $"Catalogue : {expanded.Rules.Count} règles."
            : $"Catalogue : {expanded.Rules.Count} règles, {expanded.Discarded} variantes invalides écartées.");

        return expanded.Rules;
    }

    private sealed class ConsoleWalkForwardProgress(IConsoleWriter output) : IProgress<WalkForwardProgress>
    {
        private readonly Lock _gate = new();
        private (int Window, WalkForwardStage Stage, long Evaluated) _shown;

        public void Report(WalkForwardProgress value)
        {
            lock (_gate)
            {
                var current = (value.Window, value.Stage, value.Optimization.Evaluated);
                if (current.CompareTo(_shown) <= 0)
                {
                    return;
                }

                _shown = current;
                var optimization = value.Optimization;
                var stage = value.Stage == WalkForwardStage.Screening ? "criblage" : "combinaisons";
                output.Write($"\rFenêtre {value.Window}/{value.WindowCount}, {stage} — {optimization.Evaluated:N0} / {optimization.Planned:N0} ({(double)optimization.Evaluated / optimization.Planned:P0})   ");
            }
        }
    }

    /// <summary>
    /// Progression réécrite sur une seule ligne. Les rapports arrivent des fils de l'exploration
    /// parallèle, d'où le verrou ; <see cref="Progress{T}"/> ne convient pas, il les livrerait
    /// dans le désordre.
    /// </summary>
    private sealed class ConsoleProgress(IConsoleWriter output, RankingObjective objective) : IProgress<OptimizationProgress>
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
                    ? $" — meilleur {Ranking.Name(objective)} {candidate.Score.ToString("0.00", CultureInfo.CurrentCulture)}"
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
