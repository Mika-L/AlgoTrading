using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// Williams %R indicator
/// A momentum indicator that measures overbought and oversold levels, ranging from -100 (oversold) to 0 (overbought).
/// </summary>
public class WilliamsR : BaseIndicator
{
    private readonly Dictionary<DateTime, decimal> _williamsR;
    private readonly int _period;

    public WilliamsR(List<StockPriceHistory> prices, int period = 14) : base(prices)
    {
        _period = period;
        _williamsR = CalculateWilliamsR(prices, period);
    }

    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date, out var cachedBullish))
        {
            return cachedBullish;
        }

        // Bullish if Williams %R is below -80 (oversold)
        var isBullish = false;
        if (_williamsR.TryGetValue(date, out var value))
            isBullish = value < -80;
        indicatorResultHistories.AddBullish(date, isBullish);
        return isBullish;
    }

    public override bool IsBearish(DateTime date)
    {
        if (this.indicatorResultHistories.BearishMap.TryGetValue(date, out var cachedBearish))
        {
            return cachedBearish;
        }

        // Bearish if Williams %R is above -20 (overbought)
        var isBearish = false;
        if (_williamsR.TryGetValue(date, out var value))
            isBearish = value > -20;
        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    private Dictionary<DateTime, decimal> CalculateWilliamsR(List<StockPriceHistory> prices, int period)
    {
        var result = new Dictionary<DateTime, decimal>();
        var ordered = prices.OrderBy(p => p.Date).ToList();
        for (int i = period - 1; i < ordered.Count; i++)
        {
            var window = ordered.Skip(i - period + 1).Take(period).ToList();
            var highestHigh = window.Max(p => p.High);
            var lowestLow = window.Min(p => p.Low);
            var close = ordered[i].Close;
            var wr = highestHigh != lowestLow ? -100 * (highestHigh - close) / (highestHigh - lowestLow) : 0;
            result[ordered[i].Date] = wr;
        }
        return result;
    }
}
