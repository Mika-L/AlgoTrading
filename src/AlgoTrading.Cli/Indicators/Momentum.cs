using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// Momentum
/// A simple indicator measuring the price change over a given period.
/// </summary>
public class Momentum : BaseIndicator
{
    private readonly Dictionary<DateTime, decimal> momentum;
    private readonly int period;

    public Momentum(List<StockPriceHistory> prices, int period = 10) : base(prices)
    {
        this.period = period;
        momentum = CalculateMomentum(prices, period);
    }

    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date, out var cachedBullish))
        {
            return cachedBullish;
        }

        var isBullish = momentum.TryGetValue(date, out var value) && value > 0;
        indicatorResultHistories.AddBullish(date, isBullish);

        return isBullish;
    }

    public override bool IsBearish(DateTime date)
    {

        if (this.indicatorResultHistories.BearishMap.TryGetValue(date, out var cachedBearish))
        {
            return cachedBearish;
        }

        var isBearish = momentum.TryGetValue(date, out var value) && value < 0;
        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    private Dictionary<DateTime, decimal> CalculateMomentum(List<StockPriceHistory> prices, int period)
    {
        var result = new Dictionary<DateTime, decimal>();
        var ordered = prices.OrderBy(p => p.Date).ToList();
        for (int i = period; i < ordered.Count; i++)
        {
            var value = ordered[i].Close - ordered[i - period].Close;
            result[ordered[i].Date] = value;
        }
        return result;
    }
}
