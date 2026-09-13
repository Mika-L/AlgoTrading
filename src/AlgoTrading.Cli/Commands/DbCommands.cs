using System.CommandLine;
using AlgoTrading.Application.Ports;
using AlgoTrading.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlgoTrading.Cli.Commands;

public static class DbCommands
{
    public static Command Build(IServiceProvider services)
    {
        var db = new Command("db", "Gestion de la base locale.");

        db.Add(Migrate(services));
        db.Add(ImportLegacy(services));

        return db;
    }

    private static Command Migrate(IServiceProvider services)
    {
        var command = new Command("migrate", "Crée ou met à jour le schéma de la base.");

        command.SetAction(async (_, cancellationToken) =>
        {
            var factory = services.GetRequiredService<IDbContextFactory<AlgoTradingDbContext>>();
            var output = services.GetRequiredService<IConsoleWriter>();

            await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();

            if (pending.Count == 0)
            {
                output.WriteLine("Le schéma est déjà à jour.");
                return 0;
            }

            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            output.WriteLine($"{pending.Count} migration(s) appliquée(s) : {string.Join(", ", pending)}.");

            return 0;
        });

        return command;
    }

    /// <summary>
    /// Reprise de l'historique de l'ancienne base.
    /// <para>Commande de transition : l'ancienne base n'a pas de table d'historique de
    /// migrations — elle avait été créée hors migration — et ne peut donc pas être mise à
    /// niveau en place. C'est le plan de repli si le re-téléchargement échoue, l'endpoint
    /// de la source n'étant pas contractuel.</para>
    /// </summary>
    private static Command ImportLegacy(IServiceProvider services)
    {
        var source = new Option<FileInfo>("--source") { Description = "Fichier SQLite de l'ancienne base.", Required = true };

        var command = new Command("import-legacy", "Reprend l'historique de l'ancienne base (commande de transition).");
        command.Add(source);

        command.SetAction(async (parse, cancellationToken) =>
        {
            var file = parse.GetValue(source)!;
            var output = services.GetRequiredService<IConsoleWriter>();

            if (!file.Exists)
            {
                output.WriteWarning($"Fichier introuvable : {file.FullName}");
                return 1;
            }

            var importer = services.GetRequiredService<LegacyDatabaseImporter>();
            var report = await importer.ImportAsync(file.FullName, cancellationToken).ConfigureAwait(false);

            output.WriteLine($"{report.Instruments} instrument(s), {report.Bars} séance(s) reprises.");

            if (report.Skipped > 0)
            {
                output.WriteWarning($"{report.Skipped} séance(s) écartée(s) pour incohérence des prix.");
            }

            return 0;
        });

        return command;
    }
}
