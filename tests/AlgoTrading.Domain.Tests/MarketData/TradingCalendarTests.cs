using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.MarketData;

public class TradingCalendarTests
{
    [Fact]
    public void should_gather_every_session_of_the_universe_without_repeating_one()
    {
        var first = TestBars.FromCloses(Symbol.From("ACA.PA"), TestBars.Origin, 1m, 2m, 3m);
        var second = TestBars.FromCloses(Symbol.From("GLE.PA"), TestBars.Origin, 1m, 2m, 3m, 4m, 5m);

        var calendar = TradingCalendar.FromSeries([first, second]);

        calendar.Count.ShouldBe(5);
        calendar.First.ShouldBe(TestBars.Origin);
        calendar.Last.ShouldBe(second.LastDate);
    }

    [Fact]
    public void should_list_the_sessions_in_chronological_order()
    {
        var calendar = TradingCalendar.FromDates(
        [
            new DateOnly(2017, 3, 1),
            new DateOnly(2017, 1, 2),
            new DateOnly(2017, 2, 1),
        ]);

        calendar.Sessions.ShouldBe(
        [
            new DateOnly(2017, 1, 2),
            new DateOnly(2017, 2, 1),
            new DateOnly(2017, 3, 1),
        ]);
    }

    [Fact]
    public void should_restrict_the_calendar_to_the_requested_window()
    {
        var series = TestBars.FromCloses(1m, 2m, 3m, 4m, 5m);
        var calendar = TradingCalendar.FromSeries([series]);

        var sliced = calendar.Slice(series[1].Date, series[3].Date);

        sliced.Count.ShouldBe(3);
        sliced.First.ShouldBe(series[1].Date);
        sliced.Last.ShouldBe(series[3].Date);
    }
}
