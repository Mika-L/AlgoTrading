using AlgoTrading.Domain.Reporting;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Reporting;

public class TrialTallyTests
{
    private static TrialTally Tally(params double[] sharpes)
    {
        var dispersion = new SharpeDispersion();

        foreach (var sharpe in sharpes)
        {
            dispersion.Add(sharpe);
        }

        return dispersion.Tally;
    }

    [Fact]
    public void should_merge_two_explorations_as_if_all_their_trials_had_been_tallied_together()
    {
        var merged = Tally(0.01, 0.03, -0.02).Combine(Tally(0.04, 0.00));
        var together = Tally(0.01, 0.03, -0.02, 0.04, 0.00);

        merged.Trials.ShouldBe(5);
        merged.MeanSharpe.ShouldBe(together.MeanSharpe, 1e-15);
        merged.Variance.ShouldBe(together.Variance, 1e-15);
    }

    [Fact]
    public void should_leave_a_tally_unchanged_when_merged_with_no_trials()
    {
        var tally = Tally(0.01, 0.03);

        tally.Combine(TrialTally.Empty).ShouldBe(tally);
        TrialTally.Empty.Combine(tally).ShouldBe(tally);
    }

    [Fact]
    public void should_count_past_explorations_whose_period_overlaps_by_a_single_session()
    {
        var history = new TrialHistory(
        [
            new PastTrials(new(2017, 1, 1), new(2019, 12, 31), Tally(0.01, 0.02)),
            new PastTrials(new(2020, 1, 1), new(2020, 6, 30), Tally(0.03, 0.05, 0.04)),
            new PastTrials(new(2024, 1, 1), new(2024, 12, 31), Tally(0.10, -0.10)),
        ]);

        history.Overlapping(new(2019, 12, 31), new(2023, 12, 31)).ShouldBe(Tally(0.01, 0.02).Combine(Tally(0.03, 0.05, 0.04)));
        history.Overlapping(new(2021, 1, 1), new(2023, 12, 31)).ShouldBe(TrialTally.Empty);
    }

    [Fact]
    public void should_doubt_a_winner_more_once_past_trials_are_counted()
    {
        var winner = new ReturnMoments(0.08, 0d, 3d, 1_500);
        var exploration = new TrialTally(200, 0d, 199 * 0.0004);

        exploration.Deflate(winner, priorTrials: 20_000).ShouldBeLessThan(exploration.Deflate(winner));
    }

    [Fact]
    public void should_never_make_a_winner_more_convincing_by_running_the_same_exploration_again()
    {
        var winner = new ReturnMoments(0.08, 0d, 3d, 1_500);
        var earlier = Tally(0.09, -0.07, 0.05, -0.04);
        var exploration = Tally(0.01, 0.03, -0.02, 0.04, 0.00);

        var once = exploration.Deflate(winner, earlier.Trials);
        var twice = exploration.Deflate(winner, earlier.Combine(exploration).Trials);

        twice.ShouldBeLessThan(once);
    }
}
