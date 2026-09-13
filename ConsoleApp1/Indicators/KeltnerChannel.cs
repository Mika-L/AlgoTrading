using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// Keltner Channel indicator
/// A volatility-based envelope set above and below an exponential moving average (EMA).
/// </summary>
public class KeltnerChannel : BaseIndicator
{
    private readonly Dictionary<DateTime, (decimal Middle, decimal Upper, decimal Lower)> _channels;
    private readonly int _period;
    private readonly decimal _multiplier;

    public KeltnerChannel(List<StockPriceHistory> prices, int period = 20, decimal multiplier = 1.5m) : base(prices)
    {
        _period = period;
        _multiplier = multiplier;
        _channels = CalculateChannels(prices, period, multiplier);
    }

    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date, out var cachedBullish))
        {
            return cachedBullish;
        }

        // Bullish if price closes below lower channel (potential oversold)
        var price = prices.FirstOrDefault(p => p.Date == date);
        var isBullish = false;
        if (price != null && _channels.TryGetValue(date, out var channel))
        {
            isBullish = price.AdjustedClose < channel.Lower;
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

        // Bearish if price closes above upper channel (potential overbought)
        var price = prices.FirstOrDefault(p => p.Date == date);
        var isBearish = false;
        if (price != null && _channels.TryGetValue(date, out var channel))
        {
            isBearish = price.AdjustedClose > channel.Upper;
        }
        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    private Dictionary<DateTime, (decimal Middle, decimal Upper, decimal Lower)> CalculateChannels(List<StockPriceHistory> prices, int period, decimal multiplier)
    {
        var result = new Dictionary<DateTime, (decimal, decimal, decimal)>();
        var ordered = prices.OrderBy(p => p.Date).ToList();
        if (ordered.Count < period)
            return result;
        for (int i = period - 1; i < ordered.Count; i++)
        {
            var window = ordered.Skip(i - period + 1).Take(period).ToList();
            decimal ema = window.Average(p => p.AdjustedClose);
            decimal atr = window.Average(p => p.High - p.Low);
            decimal upper = ema + multiplier * atr;
            decimal lower = ema - multiplier * atr;
            result[ordered[i].Date] = (ema, upper, lower);
        }
        return result;
    }
}
