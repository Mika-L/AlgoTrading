using AlgoTrading.Application.Ports;
using AlgoTrading.Application.UseCases;
using AlgoTrading.Infrastructure.Persistence;
using AlgoTrading.Infrastructure.Providers;
using AlgoTrading.Infrastructure.Reporting;
using AlgoTrading.Infrastructure.Strategies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlgoTrading.Infrastructure;

/// <summary>
/// Câblage commun aux deux racines de composition — la ligne de commande et l'interface web.
/// Chacune n'ajoute que ce qui lui est propre : sa sortie, sa journalisation, son hôte.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAlgoTrading(this IServiceCollection services, IConfiguration configuration, string databasePath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        // Une fabrique et non un contexte unique : l'optimiseur enchaîne des centaines de
        // runs, et un contexte de longue vie verrait son suivi de changements gonfler sans fin.
        services.AddDbContextFactory<AlgoTradingDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));

        services.AddSingleton(TimeProvider.System);

        services.Configure<UniverseOptions>(configuration.GetSection(UniverseOptions.Section));
        services.Configure<CsvProviderOptions>(configuration.GetSection(CsvProviderOptions.Section));
        services.Configure<StrategyStoreOptions>(configuration.GetSection(StrategyStoreOptions.Section));

        services.AddScoped<IMarketDataRepository, SqliteMarketDataRepository>();
        services.AddScoped<SqliteBacktestRunStore>();
        services.AddScoped<IBacktestRunStore>(sp => sp.GetRequiredService<SqliteBacktestRunStore>());
        services.AddScoped<LegacyDatabaseImporter>();
        services.AddSingleton<IStrategyRepository, JsonFileStrategyRepository>();

        services.AddSingleton<IUniverseCatalog, UniverseCatalog>();
        services.AddSingleton<IMarketDataProvider, CsvMarketDataProvider>();

        services.AddHttpClient<YahooFinanceProvider>(client =>
        {
            client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
            client.Timeout = TimeSpan.FromSeconds(30);

            // Sans en-tête d'agent crédible, la source renvoie une erreur d'autorisation.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AlgoTrading/1.0)");
        });

        services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<YahooFinanceProvider>());

        services.AddSingleton<IReportSink, CsvReportSink>();
        services.AddSingleton<IReportSink, ScottPlotChartRenderer>();

        services.AddScoped(sp => new FetchMarketDataHandler(
            sp.GetRequiredService<IUniverseCatalog>(),
            [.. sp.GetServices<IMarketDataProvider>()],
            sp.GetRequiredService<IMarketDataRepository>(),
            sp.GetRequiredService<TimeProvider>()));

        services.AddScoped<RunBacktestHandler>();
        services.AddScoped<ReplayBacktestRunHandler>();
        services.AddScoped<OptimizeStrategyHandler>();
        services.AddScoped<RunWalkForwardHandler>();
        services.AddScoped(sp => new GenerateReportHandler([.. sp.GetServices<IReportSink>()]));

        return services;
    }
}
