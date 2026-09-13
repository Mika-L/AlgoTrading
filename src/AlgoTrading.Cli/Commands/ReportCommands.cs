using System.CommandLine;
using System.Globalization;
using AlgoTrading.Application.Ports;
using AlgoTrading.Application.UseCases;
using AlgoTrading.Domain.Reporting;
using AlgoTrading.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AlgoTrading.Cli.Commands;

public static class ReportCommands
{
    public static Command Build(IServiceProvider services)
    {
        var report = new Command("report", "Consultation et export des résultats.");

        report.Add(Show(services));
        report.Add(Chart(services));

        return report;
    }

    /// <summary>Affiche un résultat de backtest — c'est la sortie utilisateur, jamais un log.</summary>
    public static void Print(IConsoleWriter output, BacktestResult result)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(result);

        var metrics = result.Metrics;

        output.WriteLine($"{result.StrategyName}  ({result.From:yyyy-MM-dd} → {result.To:yyyy-MM-dd}, {result.Universe.Count} titre(s))");
        output.WriteLine($"Empreinte : {result.Fingerprint[..16]}…");
        output.WriteLine();

        output.WriteTable(
            ["Mesure", "Valeur"],
            [
                ["Capital de départ", ConsoleWriter.Money(result.InitialCash)],
                ["Valeur finale", ConsoleWriter.Money(result.FinalEquity)],
                ["Rendement total", ConsoleWriter.Percent(metrics.TotalReturn)],
                ["Rendement annualisé", ConsoleWriter.Percent(metrics.AnnualisedReturn)],
                ["Pire baisse", ConsoleWriter.Percent(metrics.MaxDrawdown)],
                ["Calmar", metrics.Calmar.ToString("0.00", CultureInfo.CurrentCulture)],
                ["Sharpe", metrics.Sharpe.ToString("0.00", CultureInfo.CurrentCulture)],
                ["Volatilité annualisée", ConsoleWriter.Percent(metrics.Volatility)],
                ["Trades", metrics.TradeCount.ToString(CultureInfo.CurrentCulture)],
                ["Taux de réussite", ConsoleWriter.Percent(metrics.WinRate)],
                ["Facteur de profit", metrics.ProfitFactor.ToString("0.00", CultureInfo.CurrentCulture)],
                ["Gain moyen", ConsoleWriter.Money(metrics.AverageWin)],
                ["Perte moyenne", ConsoleWriter.Money(metrics.AverageLoss)],
                ["Frais totaux", ConsoleWriter.Money(metrics.TotalCosts)],
                ["Positions ouvertes", result.OpenPositions.Count.ToString(CultureInfo.CurrentCulture)],
                ["Ordres refusés", result.RejectedOrders.Count.ToString(CultureInfo.CurrentCulture)],
            ]);

        foreach (var caveat in result.Caveats)
        {
            output.WriteWarning($"⚠ {caveat}");
        }
    }

    private static Command Show(IServiceProvider services)
    {
        var run = new Option<int>("--run") { Description = "Numéro du run à afficher.", Required = true };

        var command = new Command("show", "Affiche un résultat enregistré.");
        command.Add(run);

        command.SetAction(async (parse, cancellationToken) =>
        {
            var store = services.GetRequiredService<IBacktestRunStore>();
            var output = services.GetRequiredService<IConsoleWriter>();

            var summary = await store.GetAsync(parse.GetValue(run), cancellationToken).ConfigureAwait(false);

            if (summary is null)
            {
                output.WriteWarning($"Run {parse.GetValue(run)} introuvable.");
                return 1;
            }

            output.WriteTable(
                ["Mesure", "Valeur"],
                [
                    ["Stratégie", summary.StrategyName],
                    ["Empreinte", summary.Fingerprint[..16] + "…"],
                    ["Période", $"{summary.From:yyyy-MM-dd} → {summary.To:yyyy-MM-dd}"],
                    ["Capital de départ", ConsoleWriter.Money(summary.InitialCash)],
                    ["Valeur finale", ConsoleWriter.Money(summary.FinalEquity)],
                    ["Rendement total", ConsoleWriter.Percent(summary.TotalReturn)],
                    ["Pire baisse", ConsoleWriter.Percent(summary.MaxDrawdown)],
                    ["Calmar", summary.Calmar.ToString("0.00", CultureInfo.CurrentCulture)],
                    ["Trades", summary.TradeCount.ToString(CultureInfo.CurrentCulture)],
                    ["Exécuté le", summary.RanAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)],
                ]);

            return 0;
        });

        return command;
    }

    private static Command Chart(IServiceProvider services)
    {
        var run = new Option<int>("--run") { Description = "Numéro du run à tracer.", Required = true };
        var outputDirectory = new Option<string>("--out") { Description = "Répertoire de sortie.", DefaultValueFactory = _ => "reports" };
        var format = new Option<string[]>("--format") { Description = "Formats souhaités : csv, chart." };

        var command = new Command("chart", "Rejoue un run enregistré et exporte ses graphiques.");
        command.Add(run);
        command.Add(outputDirectory);
        command.Add(format);

        command.SetAction(async (parse, cancellationToken) =>
        {
            var output = services.GetRequiredService<IConsoleWriter>();
            var store = services.GetRequiredService<SqliteBacktestRunStore>();
            var id = parse.GetValue(run);

            var summary = await store.GetAsync(id, cancellationToken).ConfigureAwait(false);
            var strategy = await store.GetStrategyAsync(id, cancellationToken).ConfigureAwait(false);

            if (summary is null || strategy is null)
            {
                output.WriteWarning($"Run {id} introuvable, ou stratégie non enregistrée.");
                return 1;
            }

            // Le run est rejoué depuis sa stratégie : c'est ce que garantit l'empreinte.
            var response = await services.GetRequiredService<RunBacktestHandler>().HandleAsync(new RunBacktestRequest
            {
                Strategy = strategy,
                From = summary.From,
                To = summary.To,
                InitialCash = summary.InitialCash,
            }, cancellationToken).ConfigureAwait(false);

            var report = await services.GetRequiredService<GenerateReportHandler>().HandleAsync(new GenerateReportRequest
            {
                Result = response.Result,
                OutputDirectory = parse.GetValue(outputDirectory)!,
                Formats = parse.GetValue(format) ?? [],
            }, cancellationToken).ConfigureAwait(false);

            foreach (var file in report.Files)
            {
                output.WriteLine($"Écrit : {file}");
            }

            return 0;
        });

        return command;
    }
}
