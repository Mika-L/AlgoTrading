using System;
using ConsoleApp1.Models;
using ConsoleApp1.Repository;

namespace ConsoleApp1;

public class PorfolioMulti
{

    private readonly decimal initCash;
    private readonly string name;
    private OrderRepository orderRepository;
    public decimal Cash { get; private set; }
    public Dictionary<Stock, int> Stocks { get; set; } = new Dictionary<Stock, int>();
    public HashSet<PortoflioLine> PortfolioLines { get; set; } = new HashSet<PortoflioLine>();
    public Dictionary<Stock, List<StockPriceHistory>> StockPriceHistory { get; set; } = new Dictionary<Stock, List<StockPriceHistory>>();
    public decimal Profit { get; private set; }
    public decimal Performance => Profit / initCash * 100;


    public PorfolioMulti(string name, decimal value, Dictionary<Stock, List<StockPriceHistory>> stockPriceHistory, OrderRepository orderRepository)
    {
        this.name = name;
        Cash = value;
        initCash = value;
        StockPriceHistory = stockPriceHistory;
        this.orderRepository = orderRepository;
    }

    public void Reset()
    {
        Cash = initCash;
        Stocks.Clear();
    }

    public decimal GetNetWorth()
    {
        decimal netWorth = Cash;
        foreach (var stock in Stocks.Keys)
        {
            if (StockPriceHistory.TryGetValue(stock, out var history) && history.Any())
            {
                var latestPrice = history
                    .Where(h => h.AdjustedClose > 0)
                    .OrderByDescending(h => h.Date)
                    .First()
                    .AdjustedClose;

                netWorth += GetPositionValue(stock, latestPrice);
            }
        }

        return netWorth;
    }

    public decimal GetNetWorth(DateTime date)
    {
        decimal netWorth = Cash;
        foreach (var stock in Stocks.Keys)
        {
            if (StockPriceHistory.TryGetValue(stock, out var history) && history.Any(h => h.Date <= date))
            {
                var latestPrice = history
                    .Where(h => h.Date <= date && h.AdjustedClose > 0)
                    .OrderByDescending(h => h.Date)
                    .First().AdjustedClose;
                netWorth += GetPositionValue(stock, latestPrice);
            }
        }

        return netWorth;
    }

    public void CalculatePerf()
    {
        var netWorth = GetNetWorth();
        Profit = netWorth - initCash;
    }

    public void CalculatePerf(DateTime date)
    {
        var netWorth = GetNetWorth(date);
        Profit = netWorth - initCash;
    }

    public void Buy(Stock stock, decimal StockPrice, decimal totalAmount)
    {
        if (StockPrice <= 0)
        {
            return;
        }

        int stocksToBuy = Math.Min(MaxPossibleBuyStocks(StockPrice), (int)(totalAmount / StockPrice));

        Cash -= stocksToBuy * StockPrice;

        if (Cash < 0)
        {
            throw new InvalidOperationException("Not enough cash to buy stocks.");
        }

        if (Stocks.TryGetValue(stock, out int currentStocks))
        {
            Stocks[stock] = currentStocks + stocksToBuy;
        }
        else
        {
            Stocks[stock] = stocksToBuy;
        }
    }

    public void Buy(Stock stock, DateTime date, decimal stockPrice, decimal totalAmount)
    {
        if (stockPrice <= 0)
        {
            return;
        }

        if (IsAnomaly(stock, date, stockPrice))
        {
            return; // Skip buying if it's an anomaly
        }

        int stocksToBuy = Math.Min(MaxPossibleBuyStocks(stockPrice), (int)(totalAmount / stockPrice));

        Cash -= stocksToBuy * stockPrice;

        if (Cash < 0)
        {
            throw new InvalidOperationException("Not enough cash to buy stocks.");
        }

        if (Stocks.TryGetValue(stock, out int currentStocks))
        {
            Stocks[stock] = currentStocks + stocksToBuy;
        }
        else
        {
            Stocks[stock] = stocksToBuy;
        }

        if (stocksToBuy > 0)
        {
            orderRepository.Add(new Order
            {
                StockId = stock.Id,
                Type = OrderType.Buy,
                Price = stockPrice,
                Quantity = stocksToBuy,
                Date = date
            });
        }
    }

    public void Sell(Stock stock, decimal price)
    {
        if (price <= 0)
        {
            return;
        }

        if (stock.Symbol == "ATO.PA" && price > 100)
        {
            return; // Skip selling ATO.PA if price is above 100
        }

        if (Stocks.TryGetValue(stock, out int stocksToSell))
        {
            Cash += stocksToSell * price;
            Stocks[stock] = 0;
        }
    }

    public void Sell(Stock stock, DateTime date, decimal price)
    {
        if (price <= 0)
        {
            return;
        }

        if (IsAnomaly(stock, date, price))
        {
            return; // Skip selling if it's an anomaly
        }

        if (Stocks.TryGetValue(stock, out int stocksToSell))
        {
            Cash += stocksToSell * price;
            Stocks[stock] = 0;
        }

        if (stocksToSell > 0)
        {
            orderRepository.Add(new Order
            {
                StockId = stock.Id,
                Type = OrderType.Sell,
                Price = price,
                Quantity = stocksToSell,
                Date = date
            });
        }
    }

    public bool IsAnomaly(Stock stock, DateTime date, decimal price)
    {
        /*
        if (StockPriceHistory.TryGetValue(stock, out var history))
        {
            int maLength = 20; // Moving average length
            var priceHistory = history
                .Where(h => h.Date <= date)
                .OrderByDescending(h => h.Date)
                .Take(maLength)
                .ToList();

            if (priceHistory.Count == maLength)
            {
                var ma20 = priceHistory.Average(h => h.AdjustedClose);
                if (ma20 > 0)
                {
                    var deviation = Math.Abs(price - ma20) / ma20;
                    return deviation > 1.0m; // > 100%
                }
            }
        }
*/
        return false;
    }

    public decimal GetPositionValue(Stock stock, decimal price)
    {
        if (Stocks.TryGetValue(stock, out int stockCount))
        {
            return stockCount * price;
        }
        else
        {
            return 0;
        }
    }

    public int MaxPossibleBuyStocks(decimal price)
    {
        return price > 0 ? (int)(Cash / price) : 0;
    }

    public override string ToString()
    {
        return $"{name}|${Profit}|{Performance:0.00}%";
    }

    public bool IsDrawdown(Stock stock, DateTime date)
    {
        // Returns true if the price at the given date is below the previous maximum (i.e., in drawdown)
        if (!StockPriceHistory.TryGetValue(stock, out var history))
            return false;

        var ordered = history.Where(h => h.Date <= date).OrderBy(h => h.Date).ToList();
        if (!ordered.Any())
            return false;

        var current = ordered.Last().AdjustedClose;
        var previousMax = ordered.Max(h => h.AdjustedClose);

        // Drawdown if current price is below the previous max
        return current < previousMax;
    }
}