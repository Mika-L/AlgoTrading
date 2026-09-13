using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Backtesting;

public class PortfolioTests
{
    private static readonly Symbol Acme = Symbol.From("ACA.PA");
    private static readonly DateOnly Day = new(2017, 1, 2);

    [Fact]
    public void should_average_the_cost_of_two_purchases_at_different_prices()
    {
        var position = new Position(Acme).Buy(10, 100m).Buy(10, 120m);

        position.Quantity.ShouldBe(20);
        position.AveragePrice.ShouldBe(110m);
    }

    [Fact]
    public void should_leave_the_cost_price_untouched_when_selling_part_of_a_line()
    {
        // Le code d'origine retranchait prix × quantité au lieu de PRU × quantité :
        // le prix de revient dérivait à chaque cession.
        var position = new Position(Acme).Buy(10, 100m).Sell(4);

        position.Quantity.ShouldBe(6);
        position.AveragePrice.ShouldBe(100m);
    }

    [Fact]
    public void should_reset_the_cost_price_to_zero_without_dividing_by_zero_when_a_line_is_closed()
    {
        var position = new Position(Acme).Buy(10, 100m).Sell(10);

        position.Quantity.ShouldBe(0);
        position.AveragePrice.ShouldBe(0m);
        position.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public void should_refuse_to_sell_more_than_the_line_holds()
    {
        var position = new Position(Acme).Buy(5, 100m);

        Should.Throw<InvalidOperationException>(() => position.Sell(6));
    }

    [Fact]
    public void should_leave_the_portfolio_untouched_when_an_order_exceeds_the_available_cash()
    {
        // Le code d'origine faisait `Cash -= …` puis `if (Cash < 0) throw` : chaque exception
        // rattrapée laissait derrière elle un portefeuille incohérent.
        var portfolio = new Portfolio(1_000m);
        var tooBig = new Execution(Day, Acme, OrderSide.Buy, 100, 50m, 5m, ExecutionReason.Signal);

        portfolio.CanApply(tooBig, out var reason).ShouldBeFalse();
        reason.ShouldBe(RejectionReason.InsufficientCash);

        Should.Throw<InvalidOperationException>(() => portfolio.Apply(tooBig));

        portfolio.Cash.ShouldBe(1_000m);
        portfolio.Positions.ShouldBeEmpty();
    }

    [Fact]
    public void should_move_cash_into_a_position_when_buying()
    {
        var portfolio = new Portfolio(10_000m);

        portfolio.Apply(new Execution(Day, Acme, OrderSide.Buy, 10, 100m, 1m, ExecutionReason.Signal));

        portfolio.Cash.ShouldBe(8_999m);
        portfolio.PositionOf(Acme).Quantity.ShouldBe(10);
        portfolio.Equity(new Dictionary<Symbol, decimal> { [Acme] = 100m }).ShouldBe(9_999m);
    }

    [Fact]
    public void should_drop_the_line_from_the_portfolio_once_it_is_fully_sold()
    {
        var portfolio = new Portfolio(10_000m);
        portfolio.Apply(new Execution(Day, Acme, OrderSide.Buy, 10, 100m, 0m, ExecutionReason.Signal));

        portfolio.Apply(new Execution(Day, Acme, OrderSide.Sell, 10, 110m, 0m, ExecutionReason.Signal));

        portfolio.Holds(Acme).ShouldBeFalse();
        portfolio.Cash.ShouldBe(10_100m);
    }

    [Fact]
    public void should_refuse_to_sell_a_line_it_does_not_hold()
    {
        var portfolio = new Portfolio(10_000m);

        portfolio.CanApply(new Execution(Day, Acme, OrderSide.Sell, 1, 100m, 0m, ExecutionReason.Signal), out var reason).ShouldBeFalse();
        reason.ShouldBe(RejectionReason.NoPosition);
    }
}
