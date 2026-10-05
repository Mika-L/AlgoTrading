using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Reporting;
using Microsoft.EntityFrameworkCore;

namespace AlgoTrading.Infrastructure.Persistence;

public sealed class SqliteExplorationJournal(
    IDbContextFactory<AlgoTradingDbContext> contextFactory,
    TimeProvider clock) : IExplorationJournal
{
    public async Task<TrialHistory> HistoryAsync(IReadOnlyList<Symbol> universe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(universe);

        var key = Key(universe);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var past = await context.Explorations
            .AsNoTracking()
            .Where(e => e.Universe == key)
            .Select(e => new PastTrials(e.From, e.To, new TrialTally(e.Trials, e.MeanSharpe, e.SquaredDeviations)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TrialHistory(past);
    }

    public async Task RecordAsync(IReadOnlyList<ExplorationEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var ranAt = clock.GetUtcNow();

        context.Explorations.AddRange(entries.Select(entry => new ExplorationRow
        {
            Kind = entry.Kind.ToString(),
            Universe = Key(entry.Universe),
            From = entry.From,
            To = entry.To,
            Catalog = entry.Catalog,
            Objective = entry.Objective.ToString(),
            Trials = entry.Trials.Trials,
            MeanSharpe = entry.Trials.MeanSharpe,
            SquaredDeviations = entry.Trials.SquaredDeviations,
            Best = entry.Best,
            BestDeflatedSharpe = entry.BestDeflatedSharpe,
            RanAt = ranAt,
        }));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ExplorationEntry>> ListAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Trier sur l'identifiant, pas sur RanAt : SQLite ne sait pas ordonner un DateTimeOffset.
        var rows = await context.Explorations
            .AsNoTracking()
            .OrderByDescending(e => e.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(static row => new ExplorationEntry(
                Enum.Parse<ExplorationKind>(row.Kind),
                [.. row.Universe.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Symbol.From)],
                row.From,
                row.To,
                new TrialTally(row.Trials, row.MeanSharpe, row.SquaredDeviations))
            {
                Id = row.Id,
                RanAt = row.RanAt,
                Catalog = row.Catalog,
                Objective = Enum.Parse<RankingObjective>(row.Objective),
                Best = row.Best,
                BestDeflatedSharpe = row.BestDeflatedSharpe,
            }),
        ];
    }

    /// <summary>Les tickers triés : l'ordre de chargement ne fait pas un autre univers.</summary>
    private static string Key(IReadOnlyList<Symbol> universe) =>
        string.Join(',', universe.Select(static s => s.Ticker).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
}
