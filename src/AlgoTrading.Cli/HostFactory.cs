using AlgoTrading.Application.Ports;
using AlgoTrading.Application.UseCases;
using AlgoTrading.Infrastructure.Persistence;
using AlgoTrading.Infrastructure.Providers;
using AlgoTrading.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlgoTrading.Cli;

/// <summary>Racine de composition : le seul endroit du projet qui connaisse toutes les couches.</summary>
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

        // Une fabrique et non un contexte unique : l'optimiseur enchaîne des centaines de
        // runs, et un contexte de longue vie verrait son suivi de changements gonfler sans fin.
        builder.Services.AddDbContextFactory<AlgoTradingDbContext>(options => options.UseSqlite($"Data Source={database}"));

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IConsoleWriter, ConsoleWriter>();

        builder.Services.Configure<UniverseOptions>(builder.Configuration.GetSection(UniverseOptions.Section));
        builder.Services.Configure<CsvProviderOptions>(builder.Configuration.GetSection(CsvProviderOptions.Section));

        builder.Services.AddScoped<IMarketDataRepository, SqliteMarketDataRepository>();
        builder.Services.AddScoped<SqliteBacktestRunStore>();
        builder.Services.AddScoped<IBacktestRunStore>(sp => sp.GetRequiredService<SqliteBacktestRunStore>());
        builder.Services.AddScoped<LegacyDatabaseImporter>();

        builder.Services.AddSingleton<IUniverseCatalog, UniverseCatalog>();
        builder.Services.AddSingleton<IMarketDataProvider, CsvMarketDataProvider>();

        builder.Services.AddHttpClient<YahooFinanceProvider>(client =>
        {
            client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
            client.Timeout = TimeSpan.FromSeconds(30);

            // Sans en-tête d'agent crédible, la source renvoie une erreur d'autorisation.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AlgoTrading/1.0)");
        });

        builder.Services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<YahooFinanceProvider>());

        builder.Services.AddSingleton<IReportSink, CsvReportSink>();
        builder.Services.AddSingleton<IReportSink, ScottPlotChartRenderer>();

        builder.Services.AddScoped(sp => new FetchMarketDataHandler(
            sp.GetRequiredService<IUniverseCatalog>(),
            [.. sp.GetServices<IMarketDataProvider>()],
            sp.GetRequiredService<IMarketDataRepository>(),
            sp.GetRequiredService<TimeProvider>()));

        builder.Services.AddScoped<RunBacktestHandler>();
        builder.Services.AddScoped<OptimizeStrategyHandler>();
        builder.Services.AddScoped(sp => new GenerateReportHandler([.. sp.GetServices<IReportSink>()]));

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
