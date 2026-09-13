public class IndicatorResultHistory
{
    public readonly Dictionary<DateTime, bool> BullishMap = new();
    public readonly Dictionary<DateTime, bool> BearishMap = new();

    public void AddBullish(DateTime date, bool isBullish)
    {
        BullishMap[date.Date] = isBullish;
    }

    public void AddBearish(DateTime date, bool isBearish)
    {
        BearishMap[date.Date] = isBearish;
    }

    public bool IsBullish(DateTime date)
    {
        return BullishMap.TryGetValue(date.Date, out bool result) && result;
    }

    public bool IsBearish(DateTime date)
    {
        return BearishMap.TryGetValue(date.Date, out bool result) && result;
    }
}