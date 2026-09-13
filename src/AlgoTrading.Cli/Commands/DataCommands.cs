using System.CommandLine;
using System.Globalization;
using AlgoTrading.Application.Ports;
using AlgoTrading.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace AlgoTrading.Cli.Commands;

public static class DataCommands
{
    public static Command Build(IServiceProvider services)
    {
        var data = new Command("data", "Téléchargement et inspection des cotations.");

        data.Add(Fetch(services));
        data.Add(List(services));
        data.Add(Gaps(services));

        return data;
    }

    private static Command Fetch(IServiceProvider services)
    {
        var universe = new Option<string>("--universe") { Description = "Nom d'un univers configuré, ou liste de tickers séparés par des virgules.", Required = true };
        var from = new Option<DateOnly>("--from") { Description = "Première séance à télécharger.", Required = true };
        var to = new Option<DateOnly?>("--to") { Description = "Dernière séance ; par défaut, aujourd'hui." };
        var provider = new Option<string>("--provider") { Description = "Source des cotations : yahoo ou csv.", DefaultValueFactory = _ => "yahoo" };

        var command = new Command("fetch", "Télécharge un univers et l'enregistre en base.");
        command.Add(universe);
        command.Add(from);
        command.Add(to);
        command.Add(provider);

        command.SetAction(async (parse, cancellationToken) =>
        {
            var handler = services.GetRequiredService<FetchMarketDataHandler>();
            var output = services.GetRequiredService<IConsoleWriter>();

            var result = await handler.HandleAsync(new FetchMarketDataRequest
            {
                Universe = parse.GetValue(universe)!,
                From = parse.GetValue(from),
                To = parse.GetValue(to),
                Provider = parse.GetValue(provider)!,
            }, cancellationToken).ConfigureAwait(false);

            output.WriteLine($"{result.InstrumentsFetched} instrument(s) mis à jour, {result.BarsSaved} séance(s) ajoutée(s).");

            foreach (var failure in result.Failures)
            {
                output.WriteWarning(failure);
            }

            return result.InstrumentsFetched == 0 ? 1 : 0;
        });

        return command;
    }

    private static Command List(IServiceProvider services)
    {
        var command = new Command("list", "Liste les instruments et l'étendue de leur historique.");

        command.SetAction(async (_, cancellationToken) =>
        {
            var repository = services.GetRequiredService<IMarketDataRepository>();
            var output = services.GetRequiredService<IConsoleWriter>();

            var instruments = await repository.ListInstrumentsAsync(cancellationToken).ConfigureAwait(false);

            if (instruments.Count == 0)
            {
                output.WriteLine("Aucun instrument en base. Lancez « algo data fetch ».");
                return 0;
            }

            output.WriteTable(
                ["Ticker", "Nom", "Séances", "Début", "Fin"],
                [
                    .. instruments.Select(i => new[]
                    {
                        i.Symbol.Ticker,
                        i.Name,
                        i.BarCount.ToString(CultureInfo.CurrentCulture),
                        i.FirstBar?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—",
                        i.LastBar?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—",
                    }),
                ]);

            return 0;
        });

        return command;
    }

    private static Command Gaps(IServiceProvider services)
    {
        var symbol = new Option<string?>("--symbol") { Description = "Se limiter à un instrument." };

        var command = new Command("gaps", "Signale les interruptions d'historique.");
        command.Add(symbol);

        command.SetAction(async (parse, cancellationToken) =>
        {
            var repository = services.GetRequiredService<IMarketDataRepository>();
            var output = services.GetRequiredService<IConsoleWriter>();

            var ticker = parse.GetValue(symbol);
            var symbolFilter = string.IsNullOrWhiteSpace(ticker) ? null : (AlgoTrading.Domain.MarketData.Symbol?)AlgoTrading.Domain.MarketData.Symbol.From(ticker);

            var gaps = await repository.FindGapsAsync(symbolFilter, cancellationToken).ConfigureAwait(false);
            var breaks = await repository.FindDiscontinuitiesAsync(symbolFilter, cancellationToken).ConfigureAwait(false);

            if (gaps.Count == 0 && breaks.Count == 0)
            {
                output.WriteLine("Aucune interruption d'historique, aucune rupture de cours.");
                return 0;
            }

            if (gaps.Count > 0)
            {
                output.WriteLine("Séances manquantes");
                output.WriteTable(
                    ["Ticker", "Depuis", "Jusqu'à", "Séances"],
                    [
                        .. gaps.Select(g => new[]
                        {
                            g.Symbol.Ticker,
                            g.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            g.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            g.MissingSessions.ToString(CultureInfo.CurrentCulture),
                        }),
                    ]);
            }

            if (breaks.Count > 0)
            {
                if (gaps.Count > 0)
                {
                    output.WriteLine();
                }

                output.WriteLine("Ruptures de cours — opération sur titre non répercutée dans l'historique");
                output.WriteTable(
                    ["Ticker", "Séance", "Veille", "Clôture", "Rapport"],
                    [
                        .. breaks.Select(b => new[]
                        {
                            b.Symbol.Ticker,
                            b.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            b.PreviousClose.ToString("N2", CultureInfo.CurrentCulture),
                            b.Close.ToString("N2", CultureInfo.CurrentCulture),
                            "× " + b.Ratio.ToString("N1", CultureInfo.CurrentCulture),
                        }),
                    ]);

                output.WriteWarning("Écartez ces titres par « Universes:Exclusions », ou corrigez leurs données : laissés tels quels, ils fabriquent une plus-value fictive.");
            }

            return 0;
        });

        return command;
    }
}
