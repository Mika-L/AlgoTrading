using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Reporting;
using AlgoTrading.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AlgoTrading.Web.Tests.Persistence;

public sealed class SqliteExplorationJournalTests : IDisposable
{
    private static readonly Symbol[] Cac = [Symbol.From("BNP.PA"), Symbol.From("AIR.PA"), Symbol.From("MC.PA")];

    private readonly string _path = Path.Combine(Path.GetTempPath(), "algotrading-journal-" + Guid.NewGuid().ToString("N") + ".db");
    private readonly SqliteExplorationJournal _journal;

    public SqliteExplorationJournalTests()
    {
        var factory = new Factory(new DbContextOptionsBuilder<AlgoTradingDbContext>().UseSqlite($"Data Source={_path}").Options);

        using (var context = factory.CreateDbContext())
        {
            context.Database.Migrate();
        }

        _journal = new SqliteExplorationJournal(factory, TimeProvider.System);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    [Fact]
    public async Task should_count_past_trials_on_the_same_stocks_whatever_order_they_were_loaded_in()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var tally = new TrialTally(152, 0.012, 0.31);

        await _journal.RecordAsync([Entry(Cac, new(2017, 1, 2), new(2023, 12, 29), tally)], cancellation);

        var history = await _journal.HistoryAsync([.. Cac.Reverse()], cancellation);

        history.Overlapping(new(2020, 1, 1), new(2020, 12, 31)).ShouldBe(tally);
        history.Overlapping(new(2024, 1, 1), new(2024, 12, 31)).ShouldBe(TrialTally.Empty);
    }

    [Fact]
    public async Task should_not_count_trials_made_on_other_stocks()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await _journal.RecordAsync([Entry(Cac[..2], new(2017, 1, 2), new(2023, 12, 29), new TrialTally(152, 0.012, 0.31))], cancellation);

        (await _journal.HistoryAsync(Cac, cancellation)).Explorations.ShouldBeEmpty();
    }

    [Fact]
    public async Task should_list_explorations_from_the_most_recent()
    {
        var cancellation = TestContext.Current.CancellationToken;

        await _journal.RecordAsync([Entry(Cac, new(2017, 1, 2), new(2019, 12, 31), new TrialTally(10, 0d, 0d))], cancellation);
        await _journal.RecordAsync(
            [
                Entry(Cac, new(2018, 1, 2), new(2020, 12, 31), new TrialTally(3_000, 0.01, 0.2)) with
                {
                    Catalog = "screening",
                    Objective = RankingObjective.InformationRatio,
                    Best = "Bollinger(20, 2.5)",
                    BestDeflatedSharpe = 0.78m,
                },
            ],
            cancellation);

        var listed = await _journal.ListAsync(cancellationToken: cancellation);

        listed.Select(static e => e.Trials.Trials).ShouldBe([3_000L, 10L]);
        listed[0].ShouldSatisfyAllConditions(
            e => e.Kind.ShouldBe(ExplorationKind.Screening),
            e => e.Universe.ShouldBe([Symbol.From("AIR.PA"), Symbol.From("BNP.PA"), Symbol.From("MC.PA")]),
            e => e.Catalog.ShouldBe("screening"),
            e => e.Objective.ShouldBe(RankingObjective.InformationRatio),
            e => e.Best.ShouldBe("Bollinger(20, 2.5)"),
            e => e.BestDeflatedSharpe.ShouldBe(0.78m));
    }

    private static ExplorationEntry Entry(IReadOnlyList<Symbol> universe, DateOnly from, DateOnly to, TrialTally trials) =>
        new(ExplorationKind.Screening, universe, from, to, trials);

    private sealed class Factory(DbContextOptions<AlgoTradingDbContext> options) : IDbContextFactory<AlgoTradingDbContext>
    {
        public AlgoTradingDbContext CreateDbContext() => new(options);
    }
}
