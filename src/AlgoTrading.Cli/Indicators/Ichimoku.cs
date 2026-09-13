using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// Ichimoku Kinko Hyo indicator
/// A comprehensive indicator that defines support/resistance, trend direction, and momentum.
/// https://finary.com/fr/glossaire/ichimoku
/// </summary>
public class Ichimoku : BaseIndicator
{
    private readonly Dictionary<DateTime, decimal> _tenkan;
    private readonly Dictionary<DateTime, decimal> _kijun;
    private readonly Dictionary<DateTime, decimal> _senkouA;
    private readonly Dictionary<DateTime, decimal> _senkouB;
    private readonly Dictionary<DateTime, decimal> _chikou;
    private readonly int _tenkanPeriod;
    private readonly int _kijunPeriod;
    private readonly int _senkouBPeriod;
    private readonly int _displacement;

    public Ichimoku(List<StockPriceHistory> prices, int tenkanPeriod = 9, int kijunPeriod = 26, int senkouBPeriod = 52, int displacement = 26) : base(prices)
    {
        _tenkanPeriod = tenkanPeriod;
        _kijunPeriod = kijunPeriod;
        _senkouBPeriod = senkouBPeriod;
        _displacement = displacement;
        _tenkan = CalculateLine(prices, tenkanPeriod);
        _kijun = CalculateLine(prices, kijunPeriod);
        _senkouA = CalculateSenkouA(_tenkan, _kijun, displacement);
        _senkouB = CalculateSenkouB(prices, senkouBPeriod, displacement);
        _chikou = CalculateChikou(prices, displacement);
    }

    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date, out var cachedBullish))
        {
            return cachedBullish;
        }

        // Bullish if price is above Kijun and SenkouA > SenkouB
        var price = prices.FirstOrDefault(p => p.Date == date)?.AdjustedClose ?? 0m;
        var isBullish = false;
        if (_kijun.TryGetValue(date, out var kijun) && _senkouA.TryGetValue(date, out var senkouA) && _senkouB.TryGetValue(date, out var senkouB))
        {
            isBullish = price > kijun && senkouA > senkouB;
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

        // Bearish if price is below Kijun and SenkouA < SenkouB
        var price = prices.FirstOrDefault(p => p.Date == date)?.AdjustedClose ?? 0m;
        var isBearish = false;
        if (_kijun.TryGetValue(date, out var kijun) && _senkouA.TryGetValue(date, out var senkouA) && _senkouB.TryGetValue(date, out var senkouB))
        {
            isBearish = price < kijun && senkouA < senkouB;
        }
        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    private Dictionary<DateTime, decimal> CalculateLine(List<StockPriceHistory> prices, int period)
    {
        var result = new Dictionary<DateTime, decimal>();
        var ordered = prices.OrderBy(p => p.Date).ToList();
        for (int i = period - 1; i < ordered.Count; i++)
        {
            var window = ordered.Skip(i - period + 1).Take(period).ToList();
            var high = window.Max(p => p.High);
            var low = window.Min(p => p.Low);
            result[ordered[i].Date] = (high + low) / 2m;
        }
        return result;
    }

    private Dictionary<DateTime, decimal> CalculateSenkouA(Dictionary<DateTime, decimal> tenkan, Dictionary<DateTime, decimal> kijun, int displacement)
    {
        var result = new Dictionary<DateTime, decimal>();
        foreach (var date in tenkan.Keys.Intersect(kijun.Keys))
        {
            var futureDate = date.AddDays(displacement);
            result[futureDate] = (tenkan[date] + kijun[date]) / 2m;
        }
        return result;
    }

    private Dictionary<DateTime, decimal> CalculateSenkouB(List<StockPriceHistory> prices, int period, int displacement)
    {
        var result = new Dictionary<DateTime, decimal>();
        var ordered = prices.OrderBy(p => p.Date).ToList();
        for (int i = period - 1; i < ordered.Count; i++)
        {
            var window = ordered.Skip(i - period + 1).Take(period).ToList();
            var high = window.Max(p => p.High);
            var low = window.Min(p => p.Low);
            var futureDate = ordered[i].Date.AddDays(displacement);
            result[futureDate] = (high + low) / 2m;
        }
        return result;
    }

    private Dictionary<DateTime, decimal> CalculateChikou(List<StockPriceHistory> prices, int displacement)
    {
        var result = new Dictionary<DateTime, decimal>();
        var ordered = prices.OrderBy(p => p.Date).ToList();
        for (int i = 0; i < ordered.Count - displacement; i++)
        {
            var pastDate = ordered[i + displacement].Date;
            result[ordered[i].Date] = ordered[i + displacement].AdjustedClose;
        }
        return result;
    }
}
