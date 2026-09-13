using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// MACD (Moving Average Convergence Divergence)
/// A momentum indicator based on the difference between two exponential moving averages.
/// </summary>
public class MACD : BaseIndicator
{
    private readonly Dictionary<DateTime, decimal> macd;
    private readonly Dictionary<DateTime, decimal> signal;

    public MACD(List<StockPriceHistory> prices, int shortPeriod = 12, int longPeriod = 26, int signalPeriod = 9) : base(prices)
    {
        macd = CalculateMACD(prices, shortPeriod, longPeriod);
        signal = CalculateSignal(macd, signalPeriod);
    }

    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date, out var cachedBullish))
        {
            return cachedBullish;
        }

        var isBullish = false;
        if (macd.TryGetValue(date, out var macdValue) && signal.TryGetValue(date, out var signalValue))
            isBullish = macdValue > signalValue;
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
        if (macd.TryGetValue(date, out var macdValue) && signal.TryGetValue(date, out var signalValue))
            isBearish = macdValue < signalValue;
        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    private Dictionary<DateTime, decimal> CalculateMACD(List<StockPriceHistory> prices, int shortPeriod, int longPeriod)
    {
        var emaShort = CalculateEMA(prices, shortPeriod);
        var emaLong = CalculateEMA(prices, longPeriod);
        var result = new Dictionary<DateTime, decimal>();
        foreach (var date in emaShort.Keys.Intersect(emaLong.Keys))
        {
            result[date] = emaShort[date] - emaLong[date];
        }
        return result;
    }

    private Dictionary<DateTime, decimal> CalculateSignal(Dictionary<DateTime, decimal> macd, int period)
    {
        var result = new Dictionary<DateTime, decimal>();
        var values = macd.OrderBy(kv => kv.Key).ToList();
        if (values.Count < period) return result;
        decimal ema = values.Take(period).Average(kv => kv.Value);
        for (int i = period; i < values.Count; i++)
        {
            ema = ((values[i].Value - ema) * (2m / (period + 1))) + ema;
            result[values[i].Key] = ema;
        }
        return result;
    }

    private Dictionary<DateTime, decimal> CalculateEMA(List<StockPriceHistory> prices, int period)
    {
        var result = new Dictionary<DateTime, decimal>();
        var ordered = prices.OrderBy(p => p.Date).ToList();
        if (ordered.Count < period) return result;
        decimal ema = ordered.Take(period).Average(p => p.AdjustedClose);
        for (int i = period; i < ordered.Count; i++)
        {
            ema = ((ordered[i].AdjustedClose - ema) * (2m / (period + 1))) + ema;
            result[ordered[i].Date] = ema;
        }
        return result;
    }
}
