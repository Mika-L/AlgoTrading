using System;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/// <summary>
/// Volume Weighted Average Price (VWAP)
/// </summary>
public class VWAP : BaseIndicator
{
    private readonly Dictionary<DateTime, decimal> vwap;

    public VWAP(List<StockPriceHistory> prices) : base(prices)
    {
        vwap = CalculateVWAP(prices);
    }

    public override bool IsBearish(DateTime date)
    {
        if (this.indicatorResultHistories.BearishMap.TryGetValue(date, out var cachedBearish))
        {
            return cachedBearish;
        }

        vwap.TryGetValue(date, out decimal todayVWAP);
        var todayPrice = prices.Find(p => p.Date == date)?.AdjustedClose;
        var isBearish = false;

        if (todayPrice > todayVWAP)
        {
            var wasLow = prices
                .Where(p => p.Date >= date.AddDays(-7) && p.Date < date)
                .Any(p => p.AdjustedClose < todayVWAP);
            isBearish = todayPrice < todayVWAP * 1.01m; // <1% au-dessus du VWAP
        }
        else
        {
            isBearish = todayPrice > todayVWAP;
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

        vwap.TryGetValue(date, out decimal todayVWAP);
        var todayPrice = prices.Find(p => p.Date == date)?.AdjustedClose;
        var isBullish = false;

        // if (todayPrice > todayVWAP)
        // {
        //     return todayPrice < todayVWAP * 1.01m; // <1% au-dessus du VWAP
        // }

        isBullish = todayPrice < todayVWAP;
        indicatorResultHistories.AddBullish(date, isBullish);
        return isBullish;
    }

    /// <summary>
    /// Volume Weighted Average Price
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static Dictionary<DateTime, decimal> CalculateVWAP(List<StockPriceHistory> data)
    {
        var vwapValues = new Dictionary<DateTime, decimal>();
        decimal cumulativePV = 0;  // Somme(Prix Typique * Volume)
        decimal cumulativeVolume = 0; // Somme(Volume)

        foreach (var candle in data)
        {
            var typicalPrice = (candle.AdjustedHigh + candle.AdjustedLow + candle.AdjustedClose) / 3;
            cumulativePV += typicalPrice * candle.Volume;
            cumulativeVolume += candle.Volume;

            decimal vwap = cumulativeVolume > 0 ? cumulativePV / cumulativeVolume : 0;
            vwapValues.Add(candle.Date, vwap);
        }

        return vwapValues;
        /*
        ✅ Stratégie 1 : Achat sur repli vers VWAP (Pullback Buy)

            Le prix est au-dessus du VWAP (tendance haussière).
            Attendre un retour vers le VWAP.
            Entrer long (achat) si le prix rebondit.
            Stop-loss sous le VWAP, objectif = dernier sommet.

        ✅ Stratégie 2 : Vente sur rebond vers VWAP (Pullback Sell)

            Le prix est en dessous du VWAP (tendance baissière).
            Attendre un retour vers le VWAP.
            Entrer short (vente) si le prix est rejeté.
            Stop-loss au-dessus du VWAP, objectif = dernier creux.

        ✅ Stratégie 3 : Scalping avec le VWAP

            Acheter sous le VWAP et vendre au-dessus pour jouer les oscillations.
            Fonctionne bien sur des unités courtes (1 min, 5 min).
    */
    }
}
