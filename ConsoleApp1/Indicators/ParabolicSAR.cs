using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// Parabolic SAR indicator
/// A trend-following indicator that provides potential reversal signals.
/// </summary>
public class ParabolicSAR : BaseIndicator
{
    private readonly Dictionary<DateTime, decimal> _sar;
    private readonly decimal _step;
    private readonly decimal _maxStep;

    public ParabolicSAR(List<StockPriceHistory> prices, decimal step = 0.02m, decimal maxStep = 0.2m) : base(prices)
    {
        _step = step;
        _maxStep = maxStep;
        _sar = CalculateSAR(prices, step, maxStep);
    }

    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date, out var cachedBullish))
        {
            return cachedBullish;
        }

        // Bullish if price is above SAR
        var price = prices.FirstOrDefault(p => p.Date == date);
        var isBullish = false;
        if (price != null && _sar.TryGetValue(date, out var sarValue))
        {
            isBullish = price.AdjustedClose > sarValue;
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

        // Bearish if price is below SAR
        var price = prices.FirstOrDefault(p => p.Date == date);
        var isBearish = false;
        if (price != null && _sar.TryGetValue(date, out var sarValue))
        {
            isBearish = price.AdjustedClose < sarValue;
        }
        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    private Dictionary<DateTime, decimal> CalculateSAR(List<StockPriceHistory> prices, decimal step, decimal maxStep)
    {
        var result = new Dictionary<DateTime, decimal>();
        var ordered = prices.OrderBy(p => p.Date).ToList();
        if (ordered.Count < 2) return result;
        bool uptrend = true;
        decimal sar = ordered[0].Low;
        decimal ep = ordered[0].High;
        decimal af = step;
        for (int i = 1; i < ordered.Count; i++)
        {
            result[ordered[i].Date] = sar;
            if (uptrend)
            {
                if (ordered[i].High > ep)
                {
                    ep = ordered[i].High;
                    af = Math.Min(af + step, maxStep);
                }
                sar = sar + af * (ep - sar);
                if (ordered[i].Low < sar)
                {
                    uptrend = false;
                    sar = ep;
                    ep = ordered[i].Low;
                    af = step;
                }
            }
            else
            {
                if (ordered[i].Low < ep)
                {
                    ep = ordered[i].Low;
                    af = Math.Min(af + step, maxStep);
                }
                sar = sar + af * (ep - sar);
                if (ordered[i].High > sar)
                {
                    uptrend = true;
                    sar = ep;
                    ep = ordered[i].High;
                    af = step;
                }
            }
        }
        return result;
    }
}
