using System;
using System.Globalization;
using ConsoleApp1.Models;
using CsvHelper.Configuration;

namespace ConsoleApp1.Maps;

public class StockPriceHistoryMap : ClassMap<StockPriceHistory>
{
    public StockPriceHistoryMap()
    {
        Map(m => m.Date).Name("Date");
        Map(m => m.Close).Name("Price");
        Map(m => m.AdjustedClose).Name("Price");
        Map(m => m.Open).Name("Open");
        Map(m => m.High).Name("High");
        Map(m => m.Low).Name("Low");
        Map(m => m.Volume).Name("Vol.").Convert(row => ParseVolume(row.Row.GetField("Vol.")));
        // Map(m => m.ChangePercent).Name("Change %")
        //     .Convert(row => decimal.Parse(row.Row.GetField("Change %").TrimEnd('%'), CultureInfo.InvariantCulture)); // Retire le '%' et convertit en decimal
    }

    private static long ParseVolume(string volumeStr)
    {
        if (string.IsNullOrWhiteSpace(volumeStr))
            return 0;

        volumeStr = volumeStr.Trim();

        if (volumeStr.EndsWith("K"))
            return (long)(decimal.Parse(volumeStr.TrimEnd('K'), CultureInfo.InvariantCulture) * 1_000);
        if (volumeStr.EndsWith("M"))
            return (long)(decimal.Parse(volumeStr.TrimEnd('M'), CultureInfo.InvariantCulture) * 1_000_000);
        if (volumeStr.EndsWith("B"))
            return (long)(decimal.Parse(volumeStr.TrimEnd('B'), CultureInfo.InvariantCulture) * 1_000_000_000);

        return long.TryParse(volumeStr, out long volume) ? volume : 0;
    }
}
