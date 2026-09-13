using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Reporting;
using AlgoTrading.Domain.Strategies;
using Microsoft.EntityFrameworkCore;

namespace AlgoTrading.Infrastructure.Persistence;

public sealed class SqliteBacktestRunStore(
    IDbContextFactory<AlgoTradingDbContext> contextFactory,
    TimeProvider clock) : IBacktestRunStore
{
    public async Task<int> SaveAsync(BacktestResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var row = new BacktestRunRow
        {
            StrategyName = result.StrategyName,
            Fingerprint = result.Fingerprint,
            StrategyJson = result.StrategyJson ?? string.Empty,
            From = result.From,
            To = result.To,
            InitialCash = result.InitialCash,
            FinalEquity = result.FinalEquity,
            TotalReturn = result.Metrics.TotalReturn,
            AnnualisedReturn = result.Metrics.AnnualisedReturn,
            MaxDrawdown = result.Metrics.MaxDrawdown,
            Calmar = result.Metrics.Calmar,
            Sharpe = result.Metrics.Sharpe,
            WinRate = result.Metrics.WinRate,
            TotalCosts = result.Metrics.TotalCosts,
            TradeCount = result.Metrics.TradeCount,
            Universe = string.Join(',', result.Universe.Select(static s => s.Ticker)),
            RanAt = clock.GetUtcNow(),
        };

        context.Runs.Add(row);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return row.Id;
    }

    public async Task<IReadOnlyList<BacktestRunSummary>> ListAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var rows = await context.Runs
            .AsNoTracking()
            .OrderByDescending(r => r.RanAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(Summarise)];
    }

    public async Task<BacktestRunSummary?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var row = await context.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);

        return row is null ? null : Summarise(row);
    }

    /// <summary>Recharge la stratégie d'un run : un résultat reste rejouable à l'identique.</summary>
    public async Task<StrategyDefinition?> GetStrategyAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var json = await context.Runs
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => r.StrategyJson)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(json) ? null : StrategyDefinition.FromJson(json);
    }

    private static BacktestRunSummary Summarise(BacktestRunRow row) => new(
        row.Id,
        row.StrategyName,
        row.Fingerprint,
        row.From,
        row.To,
        row.InitialCash,
        row.FinalEquity,
        row.TotalReturn,
        row.MaxDrawdown,
        row.Calmar,
        row.TradeCount,
        row.RanAt);
}
