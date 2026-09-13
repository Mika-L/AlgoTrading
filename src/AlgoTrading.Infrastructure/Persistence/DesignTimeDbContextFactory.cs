using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlgoTrading.Infrastructure.Persistence;

/// <summary>
/// Permet à <c>dotnet ef</c> de construire le contexte sans démarrer l'application.
/// Sans elle, générer une migration exigerait un hôte complet.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AlgoTradingDbContext>
{
    public AlgoTradingDbContext CreateDbContext(string[] args)
    {
        var path = args is [var first, ..] && !string.IsNullOrWhiteSpace(first) ? first : "algotrading.db";

        var options = new DbContextOptionsBuilder<AlgoTradingDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        return new AlgoTradingDbContext(options);
    }
}
