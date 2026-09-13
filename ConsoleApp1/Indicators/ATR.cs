using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// ATR (Average True Range)
/// A volatility indicator measuring the average of the "True Range" over a given period.
/// </summary>
public class ATR : BaseIndicator
{
    private readonly Dictionary<DateTime, decimal> atr;
    private readonly int period;

    public ATR(List<StockPriceHistory> prices, int period = 14) : base(prices)
    {
        this.period = period;
        atr = CalculateATR(prices, period);
    }

    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date, out var cachedBullish))
        {
            return cachedBullish;
        }

        var isBullish = false;
        if (atr.TryGetValue(date, out var value))
        {
            isBullish = value < atr.Values.Average();
        }
        indicatorResultHistories.AddBullish(date, isBullish);
        return isBullish;
    }

    public override bool IsBearish(DateTime date)
    {
        if (this.indicatorResultHistories.BearishMap.TryGetValue(date, out var cachedBearish))
        {
            return cachedBearish;
        }

        var isBearish = false;
        if (atr.TryGetValue(date, out var value))
        {
            isBearish = value > atr.Values.Average();
        }
        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    private Dictionary<DateTime, decimal> CalculateATR(List<StockPriceHistory> prices, int period)
    {
        var result = new Dictionary<DateTime, decimal>();
        var ordered = prices.OrderBy(p => p.Date).ToList();
        if (ordered.Count < period + 1) return result;
        var trs = new List<decimal>();
        for (int i = 1; i < ordered.Count; i++)
        {
            var tr = Math.Max(ordered[i].High - ordered[i].Low, Math.Max(Math.Abs(ordered[i].High - ordered[i - 1].Close), Math.Abs(ordered[i].Low - ordered[i - 1].Close)));
            trs.Add(tr);
        }
        for (int i = period; i < trs.Count; i++)
        {
            var atrValue = trs.Skip(i - period + 1).Take(period).Average();
            result[ordered[i + 1].Date] = atrValue;
        }
        return result;
    }
}
