using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// CCI (Commodity Channel Index)
/// A momentum indicator that measures the variation of price from its moving average.
/// </summary>
public class CCI : BaseIndicator
{
    private readonly Dictionary<DateTime, decimal> cci;
    private readonly int period;

    public CCI(List<StockPriceHistory> prices, int period = 20) : base(prices)
    {
        this.period = period;
        cci = CalculateCCI(prices, period);
    }

    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date, out var cachedBullish))
        {
            return cachedBullish;
        }

        var isBullish = cci.TryGetValue(date, out var value) && value < -100;
        indicatorResultHistories.AddBullish(date, isBullish);
        return isBullish;
    }

    public override bool IsBearish(DateTime date)
    {
        if (this.indicatorResultHistories.BearishMap.TryGetValue(date, out var cachedBearish))
        {
            return cachedBearish;
        }

        var isBearish = cci.TryGetValue(date, out var value) && value > 100;
        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    private Dictionary<DateTime, decimal> CalculateCCI(List<StockPriceHistory> prices, int period)
    {
        var result = new Dictionary<DateTime, decimal>();
        var ordered = prices.OrderBy(p => p.Date).ToList();
        for (int i = period - 1; i < ordered.Count; i++)
        {
            var window = ordered.Skip(i - period + 1).Take(period).ToList();
            var typicalPrice = window.Select(p => (p.High + p.Low + p.Close) / 3).ToList();
            var sma = typicalPrice.Average();
            var meanDeviation = typicalPrice.Average(tp => Math.Abs(tp - sma));
            var cciValue = meanDeviation != 0 ? (typicalPrice.Last() - sma) / (0.015m * meanDeviation) : 0;
            result[ordered[i].Date] = cciValue;
        }
        return result;
    }
}
