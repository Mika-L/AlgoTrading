namespace AlgoTrading.Domain.Tests.Support;

/// <summary>
/// Série de cotations de l'exemple classique du RSI 14, celle que reprennent la plupart des
/// manuels. Les valeurs attendues associées ne sont <b>pas</b> recopiées d'une table publiée :
/// elles sont recalculées à la main dans le test qui les utilise, arithmétique à l'appui, et
/// corroborées indépendamment par la comparaison différentielle contre
/// <c>Skender.Stock.Indicators</c>.
/// </summary>
public static class WilderReference
{
    public static decimal[] Closes { get; } =
    [
        44.34m, 44.09m, 44.15m, 43.61m, 44.33m, 44.83m, 45.10m, 45.42m, 45.84m, 46.08m,
        45.89m, 46.03m, 45.61m, 46.28m, 46.28m, 46.00m, 46.03m, 46.41m, 46.22m, 45.64m,
        46.21m, 46.25m, 45.71m, 46.45m, 45.78m, 45.35m, 44.03m, 44.18m, 44.22m, 44.57m,
        43.42m, 42.66m, 43.13m,
    ];
}
