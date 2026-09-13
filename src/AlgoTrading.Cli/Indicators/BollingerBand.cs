using ConsoleApp1.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp1.Indicators;

/// <summary>
/// Bollinger Bands
/// The BollingerBand indicator calculates upper, middle (SMA), and lower bands for a given period and standard deviation.
/// </summary>
public class BollingerBand : BaseIndicator
{
    private readonly Dictionary<DateTime, (decimal Middle, decimal Upper, decimal Lower)> bands;
    private readonly int period;
    private readonly decimal numStdDev;

    /// <summary>
    /// BollingerBand indicator
    /// </summary>
    /// <param name="prices">List of stock prices</param>
    /// <param name="period">Number of periods to calculate the indicator</param>
    /// <param name="numStdDev">Number of standard deviations to calculate the indicator</param>
    public BollingerBand(List<StockPriceHistory> prices, int period = 20, decimal numStdDev = 2m) : base(prices)
    {
        this.period = period;
        this.numStdDev = numStdDev;
        bands = CalculateBands(this.prices, period, numStdDev);
    }

    /// <summary>
    /// IsBearish returns true if the price closes above the upper band (potential overbought).
    /// </summary>
    /// <param name="date">Date to check</param>
    /// <returns>True if the price closes above the upper band (potential overbought)</returns>
    public override bool IsBearish(DateTime date)
    {
        if (this.indicatorResultHistories.BearishMap.TryGetValue(date.Date, out var isBearish))
        {
            return isBearish;
        }

        // Bearish: Price closes above upper band (overbought)
        var price = prices.FirstOrDefault(p => p.Date == date);
        if (price != null && bands.TryGetValue(date, out var band))
        {
            isBearish = price.AdjustedClose > band.Upper;
        }
        indicatorResultHistories.AddBearish(date, isBearish);
        return isBearish;
    }

    /// <summary>
    /// IsBullish returns true if the price closes below the lower band (potential oversold).
    /// </summary>
    /// <param name="date">Date to check</param>
    /// <returns>True if the price closes below the lower band (potential oversold)</returns>
    public override bool IsBullish(DateTime date)
    {
        if (this.indicatorResultHistories.BullishMap.TryGetValue(date.Date, out var isBullish))
        {
            return isBullish;
        }

        // Bullish: Price closes below lower band (oversold)
        var price = prices.FirstOrDefault(p => p.Date == date);
        if (price != null && bands.TryGetValue(date, out var band))
        {
            isBullish = price.AdjustedClose < band.Lower;
        }
        indicatorResultHistories.AddBullish(date, isBullish);
        return isBullish;
    }

    private Dictionary<DateTime, (decimal Middle, decimal Upper, decimal Lower)> CalculateBands(List<StockPriceHistory> prices, int period, decimal numStdDev)
    {
        var result = new Dictionary<DateTime, (decimal, decimal, decimal)>();
        if (prices.Count < period)
            return result;

        for (int i = period - 1; i < prices.Count; i++)
        {
            var window = prices.Skip(i - period + 1).Take(period).Select(p => p.AdjustedClose).ToList();
            decimal sma = window.Average();
            decimal stdDev = (decimal)Math.Sqrt((double)window.Average(v => (v - sma) * (v - sma)));
            decimal upper = sma + numStdDev * stdDev;
            decimal lower = sma - numStdDev * stdDev;
            result[prices[i].Date] = (sma, upper, lower);
        }
        return result;
    }
}