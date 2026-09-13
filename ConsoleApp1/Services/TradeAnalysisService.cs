
using Microsoft.EntityFrameworkCore;

public class TradeAnalysisService
{
    private readonly TradingContext _context;

    public TradeAnalysisService(TradingContext context)
    {
        _context = context;
    }

    public List<Trade> CalculateTrades()
    {
        var trades = new List<Trade>();

        // Récupérer tous les ordres groupés par stock
        var ordersByStock = _context.Orders
            .Include(o => o.Stock)
            .OrderBy(o => o.Date)
            .GroupBy(o => o.StockId)
            .ToList();

        foreach (var stockOrders in ordersByStock)
        {
            var orders = stockOrders.OrderBy(o => o.Date).ToList();
            var position = 0;
            var buyOrders = new Queue<Order>();

            foreach (var order in orders)
            {
                if (order.Type == OrderType.Buy)
                {
                    buyOrders.Enqueue(order);
                    position += order.Quantity;
                }
                else if (order.Type == OrderType.Sell && buyOrders.Count > 0)
                {
                    var remainingSellQuantity = order.Quantity;

                    while (remainingSellQuantity > 0 && buyOrders.Count > 0)
                    {
                        var buyOrder = buyOrders.Peek();
                        var tradeQuantity = Math.Min(remainingSellQuantity, buyOrder.Quantity);

                        var trade = new Trade
                        {
                            OpenDate = buyOrder.Date,
                            CloseDate = order.Date,
                            OpenPrice = buyOrder.Price,
                            ClosePrice = order.Price,
                            Quantity = tradeQuantity,
                            StockSymbol = order.Stock.Symbol ?? $"Stock_{order.StockId}",
                            ProfitLoss = (order.Price - buyOrder.Price) * tradeQuantity
                        };

                        trades.Add(trade);

                        remainingSellQuantity -= tradeQuantity;
                        buyOrder.Quantity -= tradeQuantity;

                        if (buyOrder.Quantity == 0)
                        {
                            buyOrders.Dequeue();
                        }
                    }

                    position -= order.Quantity;
                }
            }
        }

        return trades;
    }
}