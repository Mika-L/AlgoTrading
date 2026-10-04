using AlgoTrading.Application.Ports;
using AlgoTrading.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlgoTrading.Cli;

/// <summary>Racine de composition de la ligne de commande ; le câblage commun vit dans l'infrastructure.</summary>
public static class HostFactory
{
    public static IHost Build(BootstrapOptions bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);

        // Sans args : l'analyse de la ligne de commande appartient à System.CommandLine.
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        if (!string.IsNullOrWhiteSpace(bootstrap.ConfigFile))
        {
            builder.Configuration.AddJsonFile(bootstrap.ConfigFile, optional: false, reloadOnChange: false);
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
        builder.Logging.SetMinimumLevel(ParseLevel(bootstrap.Verbosity));

        var database = bootstrap.DatabasePath
            ?? builder.Configuration.GetConnectionString("Database")
            ?? "algotrading.db";

        builder.Services.AddAlgoTrading(builder.Configuration, database);
        builder.Services.AddSingleton<IConsoleWriter, ConsoleWriter>();

        return builder.Build();
    }

    private static LogLevel ParseLevel(string verbosity) => verbosity.ToLowerInvariant() switch
    {
        "quiet" or "none" => LogLevel.None,
        "error" => LogLevel.Error,
        "warning" or "warn" => LogLevel.Warning,
        "info" or "information" => LogLevel.Information,
        "debug" => LogLevel.Debug,
        "trace" => LogLevel.Trace,
        _ => LogLevel.Warning,
    };
}
