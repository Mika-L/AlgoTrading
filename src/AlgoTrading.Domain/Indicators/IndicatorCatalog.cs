using AlgoTrading.Domain.Indicators.Catalog;

namespace AlgoTrading.Domain.Indicators;

/// <summary>
/// Fabrique les indicateurs à partir de leur descripteur. C'est le point unique qui relie
/// une stratégie sérialisée — du texte — aux recettes de calcul, sans qu'un fichier JSON
/// ait jamais à nommer un type C#.
/// </summary>
public static class IndicatorCatalog
{
    /// <summary>Les 13 indicateurs, à leurs paramètres par défaut historiques.</summary>
    public static IReadOnlyList<IIndicator> Defaults { get; } =
    [
        new ExponentialMovingAverage(),
        new Macd(),
        new Momentum(),
        new RelativeStrengthIndex(),
        new CommodityChannelIndex(),
        new StochasticOscillator(),
        new WilliamsPercentR(),
        new AverageTrueRange(),
        new BollingerBands(),
        new KeltnerChannel(),
        new Ichimoku(),
        new ParabolicSar(),
        new RollingVwap(),
    ];

    public static IReadOnlyList<string> Kinds { get; } = [.. Defaults.Select(static i => i.Descriptor.Kind).Order(StringComparer.Ordinal)];

    public static IIndicator Create(IndicatorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.Kind switch
        {
            ExponentialMovingAverage.Kind => new ExponentialMovingAverage(Int(descriptor, "fast", 9), Int(descriptor, "slow", 20)),
            Macd.Kind => new Macd(Int(descriptor, "fast", 12), Int(descriptor, "slow", 26), Int(descriptor, "signal", 9)),
            Momentum.Kind => new Momentum(Int(descriptor, "period", 10)),
            RelativeStrengthIndex.Kind => new RelativeStrengthIndex(Int(descriptor, "period", 14)),
            CommodityChannelIndex.Kind => new CommodityChannelIndex(Int(descriptor, "period", 20)),
            StochasticOscillator.Kind => new StochasticOscillator(Int(descriptor, "period", 14), Int(descriptor, "signal", 3)),
            WilliamsPercentR.Kind => new WilliamsPercentR(Int(descriptor, "period", 14)),
            AverageTrueRange.Kind => new AverageTrueRange(Int(descriptor, "period", 14)),
            BollingerBands.Kind => new BollingerBands(Int(descriptor, "period", 20), Number(descriptor, "multiplier", 2m)),
            KeltnerChannel.Kind => new KeltnerChannel(Int(descriptor, "period", 20), Number(descriptor, "multiplier", 1.5m)),
            Ichimoku.Kind => new Ichimoku(Int(descriptor, "tenkan", 9), Int(descriptor, "kijun", 26), Int(descriptor, "senkouB", 52), Int(descriptor, "displacement", 26)),
            ParabolicSar.Kind => new ParabolicSar(Number(descriptor, "step", 0.02m), Number(descriptor, "maxStep", 0.2m)),
            RollingVwap.Kind => new RollingVwap(Int(descriptor, "period", 20)),
            _ => throw new ArgumentException($"Indicateur inconnu : « {descriptor.Kind} ». Connus : {string.Join(", ", Kinds)}.", nameof(descriptor)),
        };
    }

    private static int Int(IndicatorDescriptor descriptor, string name, int fallback)
    {
        if (!descriptor.TryGetParameter(name, out var value))
        {
            return fallback;
        }

        if (decimal.Truncate(value) != value)
        {
            throw new ArgumentException($"Le paramètre « {name} » de {descriptor.Kind} doit être entier, reçu {value}.", nameof(descriptor));
        }

        return (int)value;
    }

    private static decimal Number(IndicatorDescriptor descriptor, string name, decimal fallback) =>
        descriptor.TryGetParameter(name, out var value) ? value : fallback;
}
