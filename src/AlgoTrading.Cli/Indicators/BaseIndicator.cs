using System;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

public abstract class BaseIndicator : IIndicator
{
    protected readonly List<StockPriceHistory> prices;

    public IndicatorResultHistory indicatorResultHistories;
    private readonly Dictionary<DateTime, decimal> priceMap;

    public BaseIndicator(List<StockPriceHistory> prices)
    {
        this.prices = prices.OrderBy(p => p.Date).ToList();
        indicatorResultHistories = new IndicatorResultHistory();

        // Pré-calculer une map des derniers prix valides par date
        priceMap = new Dictionary<DateTime, decimal>();
        decimal lastValid = 0;
        foreach (var p in this.prices)
        {
            if (p.AdjustedClose > 0)
                lastValid = p.AdjustedClose;

            priceMap[p.Date.Date] = lastValid;
        }
    }

    public abstract bool IsBullish(DateTime date);

    public abstract bool IsBearish(DateTime date);

    public virtual decimal GetPrice(DateTime date)
    {
        if (!priceMap.TryGetValue(date.Date, out var price))
            throw new InvalidOperationException($"Pas de prix trouvé pour {date:yyyy-MM-dd}");

        return price;
    }
}