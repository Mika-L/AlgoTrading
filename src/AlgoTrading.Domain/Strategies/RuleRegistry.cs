using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.Indicators.Catalog;
using AlgoTrading.Domain.Strategies.Rules;

namespace AlgoTrading.Domain.Strategies;

/// <summary>
/// Fabrique les règles à partir de leur configuration, et fournit pour chaque indicateur la
/// règle par défaut qui <b>reproduit le comportement historique</b> — c'est ce qui permet à
/// la première comparaison après refactor de se faire à périmètre égal.
/// </summary>
public static class RuleRegistry
{
    public static IReadOnlyList<string> Types { get; } =
    [
        ThresholdRule.Type,
        CrossoverRule.Type,
        BandBreakoutRule.Type,
        PriceVsLevelRule.Type,
        SignRule.Type,
        IchimokuCloudRule.Type,
    ];

    public static ISignalRule Create(RuleConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var descriptor = config.ToDescriptor();

        // Construire l'indicateur maintenant valide ses paramètres au plus tôt.
        _ = IndicatorCatalog.Create(descriptor);

        ISignalRule rule = config.Type switch
        {
            ThresholdRule.Type => new ThresholdRule(
                descriptor,
                config.BullishBelow ?? throw Missing(config, nameof(RuleConfig.BullishBelow)),
                config.BearishAbove ?? throw Missing(config, nameof(RuleConfig.BearishAbove)),
                config.Line ?? IndicatorLines.Value),

            CrossoverRule.Type => new CrossoverRule(
                descriptor,
                config.FastLine ?? IndicatorLines.Fast,
                config.SlowLine ?? IndicatorLines.Slow,
                config.Mode ?? CrossoverMode.Cross),

            BandBreakoutRule.Type => new BandBreakoutRule(descriptor, config.MeanReverting ?? true),

            PriceVsLevelRule.Type => new PriceVsLevelRule(
                descriptor,
                config.BullishWhenAbove ?? true,
                config.Line ?? IndicatorLines.Value),

            SignRule.Type => new SignRule(descriptor, config.Pivot ?? 0m, config.Line ?? IndicatorLines.Value),

            IchimokuCloudRule.Type => new IchimokuCloudRule(descriptor),

            _ => throw new ArgumentException($"Type de règle inconnu : « {config.Type} ». Connus : {string.Join(", ", Types)}.", nameof(config)),
        };

        return config.AsEvent ? EdgeRule.Wrap(rule) : rule;
    }

    /// <summary>
    /// Règle par défaut d'un indicateur, aux seuils qu'il portait en dur jusqu'ici.
    /// <para>L'ATR n'en a pas : il mesure une volatilité, pas une direction. Le code d'origine
    /// lui en donnait une en le comparant à la moyenne de toute la série — futur compris.
    /// Qui veut en tirer un signal doit désormais poser ses seuils explicitement.</para>
    /// </summary>
    public static RuleConfig DefaultFor(string indicatorKind) => indicatorKind switch
    {
        RelativeStrengthIndex.Kind => new RuleConfig { Type = ThresholdRule.Type, Indicator = indicatorKind, BullishBelow = 30m, BearishAbove = 70m },
        CommodityChannelIndex.Kind => new RuleConfig { Type = ThresholdRule.Type, Indicator = indicatorKind, BullishBelow = -100m, BearishAbove = 100m },
        StochasticOscillator.Kind => new RuleConfig { Type = ThresholdRule.Type, Indicator = indicatorKind, BullishBelow = 20m, BearishAbove = 80m },
        WilliamsPercentR.Kind => new RuleConfig { Type = ThresholdRule.Type, Indicator = indicatorKind, BullishBelow = -80m, BearishAbove = -20m },

        ExponentialMovingAverage.Kind => new RuleConfig { Type = CrossoverRule.Type, Indicator = indicatorKind, Mode = CrossoverMode.Cross },
        Macd.Kind => new RuleConfig
        {
            Type = CrossoverRule.Type,
            Indicator = indicatorKind,
            FastLine = IndicatorLines.Value,
            SlowLine = IndicatorLines.Signal,
            Mode = CrossoverMode.Relative,
        },

        BollingerBands.Kind => new RuleConfig { Type = BandBreakoutRule.Type, Indicator = indicatorKind, MeanReverting = true },
        KeltnerChannel.Kind => new RuleConfig { Type = BandBreakoutRule.Type, Indicator = indicatorKind, MeanReverting = true },

        ParabolicSar.Kind => new RuleConfig { Type = PriceVsLevelRule.Type, Indicator = indicatorKind, BullishWhenAbove = true },
        RollingVwap.Kind => new RuleConfig { Type = PriceVsLevelRule.Type, Indicator = indicatorKind, BullishWhenAbove = false },

        Momentum.Kind => new RuleConfig { Type = SignRule.Type, Indicator = indicatorKind, Pivot = 0m },
        Ichimoku.Kind => new RuleConfig { Type = IchimokuCloudRule.Type, Indicator = indicatorKind },

        AverageTrueRange.Kind => throw new ArgumentException(
            "L'ATR mesure une volatilité, pas une direction : il n'a pas de règle par défaut. Configurez une règle de seuil explicite si vous voulez en tirer un signal.",
            nameof(indicatorKind)),

        _ => throw new ArgumentException($"Indicateur inconnu : « {indicatorKind} ».", nameof(indicatorKind)),
    };

    /// <summary>Le catalogue par défaut : les douze indicateurs directionnels, prêts à être combinés.</summary>
    public static IReadOnlyList<RuleConfig> DefaultCatalog() =>
    [
        .. IndicatorCatalog.Kinds
            .Where(static kind => kind != AverageTrueRange.Kind)
            .Select(DefaultFor),
    ];

    private static ArgumentException Missing(RuleConfig config, string field) =>
        new($"La règle {config.Type} sur {config.Indicator} exige le réglage « {field} ».", nameof(config));
}
