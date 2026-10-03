using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Tests.Support;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Backtesting;

public class BuyAndHoldTests
{
    private static readonly BarSeries Rising = TestBars.FromOhlc(
        Symbol.From("UP"),
        TestBars.Origin,
        (100m, 100m, 100m, 100m),
        (100m, 120m, 100m, 110m),
        (110m, 130m, 110m, 120m));

    private static readonly BarSeries Falling = TestBars.FromOhlc(
        Symbol.From("DOWN"),
        TestBars.Origin,
        (50m, 50m, 40m, 40m),
        (40m, 40m, 30m, 30m),
        (30m, 30m, 25m, 25m));

    [Fact]
    public void should_split_the_capital_equally_at_the_first_opening_and_hold()
    {
        // 500 € chacun à l'ouverture : 5 titres à 100 €, 10 titres à 50 €.
        var curve = BuyAndHold.Curve([Rising, Falling], Rising.Dates[0], Rising.Dates[^1], 1_000m);

        curve.Select(static p => p.Equity).ShouldBe([(5m * 100m) + (10m * 40m), (5m * 110m) + (10m * 30m), (5m * 120m) + (10m * 25m)]);
        curve.ShouldAllBe(p => p.Cash == 0m);
    }

    [Fact]
    public void should_buy_at_the_opening_of_the_first_session_of_the_range()
    {
        var curve = BuyAndHold.Curve([Rising], Rising.Dates[1], Rising.Dates[^1], 1_000m);

        curve.Count.ShouldBe(2);
        curve[^1].Equity.ShouldBe(1_000m / 100m * 120m);
    }

    [Fact]
    public void should_ignore_a_stock_that_does_not_trade_on_the_first_session()
    {
        var late = TestBars.FromOhlc(Symbol.From("LATE"), Rising.Dates[1], (10m, 10m, 10m, 10m), (10m, 20m, 10m, 20m));

        var curve = BuyAndHold.Curve([Rising, late], Rising.Dates[0], Rising.Dates[^1], 1_000m);

        curve[^1].Equity.ShouldBe(1_000m / 100m * 120m);
    }
}
