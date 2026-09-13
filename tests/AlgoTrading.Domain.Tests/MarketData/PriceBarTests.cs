using AlgoTrading.Domain.MarketData;
using Shouldly;

namespace AlgoTrading.Domain.Tests.MarketData;

public class PriceBarTests
{
    private static readonly DateOnly Day = new(2017, 1, 2);

    [Fact]
    public void should_reject_a_session_whose_high_is_below_its_low()
    {
        Should.Throw<ArgumentException>(() => new PriceBar(Day, 10m, 9m, 11m, 10m, 1L, 10m));
    }

    [Fact]
    public void should_reject_a_session_whose_close_sits_outside_the_day_range()
    {
        Should.Throw<ArgumentException>(() => new PriceBar(Day, 10m, 11m, 9m, 12m, 1L, 12m));
    }

    [Fact]
    public void should_reject_a_session_with_a_negative_volume()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new PriceBar(Day, 10m, 11m, 9m, 10m, -1L, 10m));
    }

    [Fact]
    public void should_reject_a_session_priced_at_zero()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new PriceBar(Day, 0m, 11m, 9m, 10m, 1L, 10m));
    }

    [Fact]
    public void should_compute_the_typical_price_as_the_average_of_high_low_and_close()
    {
        var bar = new PriceBar(Day, 10m, 12m, 9m, 12m, 1L, 12m);

        bar.Typical.ShouldBe(11m);
        bar.Range.ShouldBe(3m);
    }
}
