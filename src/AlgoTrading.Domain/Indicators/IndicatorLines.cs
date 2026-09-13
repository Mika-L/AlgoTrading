namespace AlgoTrading.Domain.Indicators;

/// <summary>
/// Noms des lignes produites par les indicateurs. Les règles les désignent par ce nom,
/// ce qui rend une stratégie sérialisable sans référence à un type C#.
/// </summary>
public static class IndicatorLines
{
    public const string Value = "value";
    public const string Fast = "fast";
    public const string Slow = "slow";
    public const string Signal = "signal";
    public const string Histogram = "histogram";
    public const string Upper = "upper";
    public const string Middle = "middle";
    public const string Lower = "lower";
    public const string TenkanSen = "tenkan";
    public const string KijunSen = "kijun";
    public const string SenkouSpanA = "senkouA";
    public const string SenkouSpanB = "senkouB";
}
