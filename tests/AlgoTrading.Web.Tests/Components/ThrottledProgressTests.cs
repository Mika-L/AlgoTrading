using AlgoTrading.Web.Components.Shared;
using Shouldly;

namespace AlgoTrading.Web.Tests.Components;

public class ThrottledProgressTests
{
    [Fact]
    public void should_refresh_on_the_first_report_then_at_most_once_per_interval()
    {
        var refreshes = 0;
        var progress = new ThrottledProgress<int>(_ => { }, () => { refreshes++; return Task.CompletedTask; }, TimeSpan.FromHours(1));

        for (var i = 0; i < 100; i++)
        {
            progress.Report(i);
        }

        refreshes.ShouldBe(1);
    }

    [Fact]
    public void should_keep_every_report_even_those_not_displayed()
    {
        var last = -1;
        var progress = new ThrottledProgress<int>(value => last = value, () => Task.CompletedTask, TimeSpan.FromHours(1));

        for (var i = 0; i < 100; i++)
        {
            progress.Report(i);
        }

        last.ShouldBe(99);
    }

    [Fact]
    public void should_refresh_each_time_without_a_minimum_interval()
    {
        var refreshes = 0;
        var progress = new ThrottledProgress<int>(_ => { }, () => { refreshes++; return Task.CompletedTask; }, TimeSpan.Zero);

        progress.Report(1);
        progress.Report(2);

        refreshes.ShouldBe(2);
    }
}
