using AlgoTrading.Domain.MarketData;
using Shouldly;

namespace AlgoTrading.Domain.Tests.MarketData;

public class SymbolTests
{
    [Fact]
    public void should_treat_two_tickers_written_differently_as_the_same_instrument()
    {
        Symbol.From(" gle.pa ").ShouldBe(Symbol.From("GLE.PA"));
    }

    [Fact]
    public void should_order_instruments_alphabetically_for_a_deterministic_iteration()
    {
        Symbol[] symbols = [Symbol.From("SAN.PA"), Symbol.From("ACA.PA"), Symbol.From("GLE.PA")];

        symbols.Order().Select(static s => s.Ticker).ShouldBe(["ACA.PA", "GLE.PA", "SAN.PA"]);
    }

    [Fact]
    public void should_reject_an_instrument_without_a_ticker()
    {
        Should.Throw<ArgumentException>(() => Symbol.From("  "));
    }
}
