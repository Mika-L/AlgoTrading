using System;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// Exponential Moving Average (EMA). Moyenne Mobile Exponentielle (MME)
/// </summary>
public class MME : BaseIndicator
{
    private Dictionary<DateTime, decimal> emaShorts;
    private Dictionary<DateTime, decimal> emaLongs;

    public MME(List<StockPriceHistory> prices, int periodShort, int periodLong) : base(prices)
    {
        emaShorts = Calculate(prices, periodShort);
        emaLongs = Calculate(prices, periodLong);
    }

    private Dictionary<DateTime, decimal> Calculate(List<StockPriceHistory> prices, int period)
    {
        Dictionary<DateTime, decimal> emas = new Dictionary<DateTime, decimal>();
        if (prices.Count < period)
        {
            return emas;
        }

        decimal multiplier = 2m / (period + 1);
        decimal ema = prices.Take(period).Average(p => p.AdjustedClose);

        for (int i = period; i < prices.Count; i++)
        {
            ema = ((prices[i].AdjustedClose - ema) * multiplier) + ema;
            emas.Add(prices[i].Date, ema);
        }

        return emas;
    }

    public override bool IsBearish(DateTime date)
    {
        if (this.indicatorResultHistories.BearishMap.TryGetValue(date, out var cachedBearish))
        {
            return cachedBearish;
        }

        var isBearish = false;

        if (emaShorts.TryGetValue(date.AddDays(-1), out decimal yesterdayShort)
            && emaLongs.TryGetValue(date.AddDays(-1), out decimal yesterdayLong)
            && emaShorts.TryGetValue(date, out decimal todayShort)
            && emaLongs.TryGetValue(date, out decimal todayLong))
        {
            var yesterdayShortAboveLong = yesterdayShort > yesterdayLong;
            var todayShortBelowLong = todayShort < todayLong;

            isBearish = yesterdayShortAboveLong && todayShortBelowLong;
        }

        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date, out var cachedBullish))
        {
            return cachedBullish;
        }

        var isBullish = false;

        if (emaShorts.TryGetValue(date.AddDays(-1), out decimal yesterdayShort)
            && emaLongs.TryGetValue(date.AddDays(-1), out decimal yesterdayLong)
            && emaShorts.TryGetValue(date, out decimal todayShort)
            && emaLongs.TryGetValue(date, out decimal todayLong))
        {
            var yesterdayShortBelowLong = yesterdayShort < yesterdayLong;
            var todayShortAboveLong = todayShort > todayLong;

            return yesterdayShortBelowLong && todayShortAboveLong;
        }

        indicatorResultHistories.AddBullish(date, isBullish);
        return isBullish;
    }
}
