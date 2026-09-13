using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Backtesting;

public class TradeLedgerTests
{
    private static readonly Symbol Acme = Symbol.From("ACA.PA");

    private static Execution Buy(int day, int quantity, decimal price, decimal commission = 0m) =>
        new(new DateOnly(2017, 1, day), Acme, OrderSide.Buy, quantity, price, commission, ExecutionReason.Signal);

    private static Execution Sell(int day, int quantity, decimal price, decimal commission = 0m) =>
        new(new DateOnly(2017, 1, day), Acme, OrderSide.Sell, quantity, price, commission, ExecutionReason.Signal);

    [Fact]
    public void should_pair_a_sale_with_the_oldest_purchase_first()
    {
        var ledger = new TradeLedger();
        ledger.Record(Buy(2, 10, 100m));
        ledger.Record(Buy(3, 10, 120m));

        ledger.Record(Sell(4, 10, 130m));

        ledger.Trades.Count.ShouldBe(1);
        ledger.Trades[0].EntryPrice.ShouldBe(100m);
        ledger.Trades[0].NetPnL.ShouldBe(300m);
    }

    [Fact]
    public void should_split_a_sale_across_several_purchases_when_it_spans_them()
    {
        var ledger = new TradeLedger();
        ledger.Record(Buy(2, 10, 100m));
        ledger.Record(Buy(3, 10, 120m));

        ledger.Record(Sell(4, 15, 130m));

        ledger.Trades.Count.ShouldBe(2);
        ledger.Trades[0].Quantity.ShouldBe(10);
        ledger.Trades[1].Quantity.ShouldBe(5);
        ledger.RealizedPnL.ShouldBe(300m + 50m);
    }

    [Fact]
    public void should_charge_both_the_entry_and_the_exit_commission_to_the_trade()
    {
        var ledger = new TradeLedger();
        ledger.Record(Buy(2, 10, 100m, commission: 5m));

        ledger.Record(Sell(3, 10, 110m, commission: 5m));

        ledger.Trades[0].Costs.ShouldBe(10m);
        ledger.Trades[0].NetPnL.ShouldBe(90m);
        ledger.TotalCosts.ShouldBe(10m);
    }

    [Fact]
    public void should_split_the_entry_commission_when_only_part_of_the_lot_is_sold()
    {
        var ledger = new TradeLedger();
        ledger.Record(Buy(2, 10, 100m, commission: 10m));

        ledger.Record(Sell(3, 5, 110m, commission: 4m));

        ledger.Trades[0].Costs.ShouldBe(9m);   // 10 × 5/10 à l'entrée, 4 à la sortie
    }

    [Fact]
    public void should_tell_a_winning_trade_from_a_losing_one()
    {
        var ledger = new TradeLedger();
        ledger.Record(Buy(2, 10, 100m));
        ledger.Record(Sell(3, 10, 90m));

        ledger.Trades[0].IsWinner.ShouldBeFalse();
        ledger.RealizedPnL.ShouldBe(-100m);
    }
}
