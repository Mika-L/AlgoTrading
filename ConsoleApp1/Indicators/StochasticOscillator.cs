using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// Stochastic Oscillator
/// A momentum indicator comparing the closing price to a range of prices over a given period.
/// </summary>
public class StochasticOscillator : BaseIndicator
{
    private readonly Dictionary<DateTime, decimal> kValues;
    private readonly int period;

    public StochasticOscillator(List<StockPriceHistory> prices, int period = 14) : base(prices)
    {
        this.period = period;
        kValues = CalculateK(prices, period);
    }

    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date, out var cachedBullish))
        {
            return cachedBullish;
        }

        var isBullish = kValues.TryGetValue(date, out var k) && k < 20;
        indicatorResultHistories.AddBullish(date, isBullish);

        return isBullish;
    }

    public override bool IsBearish(DateTime date)
    {

        if (this.indicatorResultHistories.BearishMap.TryGetValue(date, out var cachedBearish))
        {
            return cachedBearish;
        }

        var isBearish = kValues.TryGetValue(date, out var k) && k > 80;
        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    private Dictionary<DateTime, decimal> CalculateK(List<StockPriceHistory> prices, int period)
    {
        var result = new Dictionary<DateTime, decimal>();
        var ordered = prices.OrderBy(p => p.Date).ToList();
        for (int i = period - 1; i < ordered.Count; i++)
        {
            var window = ordered.Skip(i - period + 1).Take(period).ToList();
            var low = window.Min(p => p.Low);
            var high = window.Max(p => p.High);
            var close = ordered[i].Close;
            var k = high != low ? 100 * (close - low) / (high - low) : 0;
            result[ordered[i].Date] = k;
        }
        return result;
    }
}
