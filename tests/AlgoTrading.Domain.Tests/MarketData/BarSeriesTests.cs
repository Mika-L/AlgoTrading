using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.MarketData;

public class BarSeriesTests
{
    [Fact]
    public void should_find_the_session_of_a_known_date()
    {
        var series = TestBars.FromCloses(10m, 11m, 12m);

        series.TryGetIndex(series[1].Date, out var index).ShouldBeTrue();
        index.ShouldBe(1);
    }

    [Fact]
    public void should_not_find_a_session_on_a_day_the_market_was_closed()
    {
        var series = TestBars.FromCloses(10m, 11m, 12m);
        var saturday = new DateOnly(2017, 1, 7);

        series.TryGetIndex(saturday, out _).ShouldBeFalse();
    }

    [Fact]
    public void should_fall_back_to_the_last_session_before_a_closed_day()
    {
        // Vendredi 6 janvier 2017 est la 5ᵉ séance ; le samedi 7 renvoie donc sur elle.
        var series = TestBars.FromCloses(1m, 2m, 3m, 4m, 5m, 6m);
        var saturday = new DateOnly(2017, 1, 7);

        var index = series.IndexAtOrBefore(saturday);

        index.ShouldBe(4);
        series[index].Date.ShouldBe(new DateOnly(2017, 1, 6));
    }

    [Fact]
    public void should_report_no_session_before_the_first_one()
    {
        var series = TestBars.FromCloses(10m, 11m);

        series.IndexAtOrBefore(new DateOnly(2016, 12, 31)).ShouldBe(-1);
    }

    [Fact]
    public void should_skip_the_weekend_when_generating_consecutive_sessions()
    {
        var series = TestBars.FromCloses(1m, 2m, 3m, 4m, 5m, 6m);

        // 5ᵉ séance = vendredi 6 janvier, 6ᵉ = lundi 9 janvier
        series[4].Date.DayOfWeek.ShouldBe(DayOfWeek.Friday);
        series[5].Date.DayOfWeek.ShouldBe(DayOfWeek.Monday);
    }

    [Fact]
    public void should_order_the_sessions_whatever_the_order_they_arrive_in()
    {
        var days = TestBars.BusinessDays(TestBars.Origin, 3);
        PriceBar[] shuffled =
        [
            new(days[2], 3m, 3m, 3m, 3m, 1L, 3m),
            new(days[0], 1m, 1m, 1m, 1m, 1L, 1m),
            new(days[1], 2m, 2m, 2m, 2m, 1L, 2m),
        ];

        var series = BarSeries.Create(Symbol.From("ACA.PA"), shuffled);

        series.Close.ToArray().ShouldBe([1m, 2m, 3m]);
    }

    [Fact]
    public void should_reject_two_sessions_sharing_the_same_date()
    {
        var day = TestBars.Origin;
        PriceBar[] duplicated =
        [
            new(day, 1m, 1m, 1m, 1m, 1L, 1m),
            new(day, 2m, 2m, 2m, 2m, 1L, 2m),
        ];

        Should.Throw<ArgumentException>(() => BarSeries.Create(Symbol.From("ACA.PA"), duplicated));
    }

    [Fact]
    public void should_restrict_the_history_to_the_requested_window()
    {
        var series = TestBars.FromCloses(1m, 2m, 3m, 4m, 5m);

        var sliced = series.Slice(series[1].Date, series[3].Date);

        sliced.Count.ShouldBe(3);
        sliced.Close.ToArray().ShouldBe([2m, 3m, 4m]);
    }

    [Fact]
    public void should_return_an_empty_history_when_the_window_is_outside_the_data()
    {
        var series = TestBars.FromCloses(1m, 2m, 3m);

        series.Slice(new DateOnly(2030, 1, 1), null).IsEmpty.ShouldBeTrue();
    }
}
