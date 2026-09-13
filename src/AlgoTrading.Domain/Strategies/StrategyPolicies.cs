namespace AlgoTrading.Domain.Strategies;

/// <summary>Politique d'agrégation : les règles et la façon de les faire voter.</summary>
public sealed record SignalPolicy
{
    public AggregationMode Mode { get; init; } = AggregationMode.Majority;

    /// <summary>Part des voix à dépasser, <b>strictement</b>. Fraction entre 0 et 1.</summary>
    public decimal Threshold { get; init; } = 0.5m;

    public IReadOnlyList<RuleConfig> Rules { get; init; } = [];

    public SignalAggregator ToAggregator() =>
        new([.. Rules.Select(static r => (RuleRegistry.Create(r), r.Weight))], Mode, Threshold);
}

public enum SizingMode
{
    /// <summary>
    /// Fraction de la <b>valeur totale du portefeuille</b>. C'est ce qui supprime la
    /// dépendance au chemin : le code d'origine prenait une fraction du cash résiduel, si
    /// bien que la première valeur acheteuse de la boucle en consommait sa part avant les
    /// suivantes et que le résultat dépendait de l'ordre d'énumération d'un dictionnaire.
    /// </summary>
    EquityFraction,

    /// <summary>Montant fixe, en euros.</summary>
    FixedNotional,
}

public sealed record PositionSizing
{
    public SizingMode Mode { get; init; } = SizingMode.EquityFraction;

    public decimal Value { get; init; } = 0.1m;
}

/// <summary>
/// Stop de protection et prise de bénéfice, exprimés en <b>fractions</b> du prix de revient
/// (<c>0,02</c> = 2 %).
/// <para>Livrés et testés mais <b>inactifs par défaut</b> : décision arbitrée, pour que le
/// premier backtest après refactor reste comparable à l'ancien.</para>
/// </summary>
public sealed record RiskPolicy
{
    public decimal? StopLoss { get; init; }

    public decimal? TakeProfit { get; init; }

    public bool IsActive => StopLoss.HasValue || TakeProfit.HasValue;

    public static RiskPolicy None { get; } = new();
}

public enum ExitMode
{
    /// <summary>Solder intégralement la position — comportement historique.</summary>
    CloseAll,

    /// <summary>N'en sortir qu'une fraction, symétrique de l'entrée.</summary>
    Fraction,
}

/// <summary>
/// Frottements et mécanique d'exécution. Les coûts sont <b>non nuls par défaut</b> : un
/// backtest à frais nuls sur quarante titres avec signal quotidien n'est pas une mesure.
/// </summary>
public sealed record ExecutionPolicy
{
    /// <summary>Commission proportionnelle, en fraction — 10 points de base par défaut.</summary>
    public decimal CommissionRate { get; init; } = 0.0010m;

    public decimal MinimumCommission { get; init; } = 1m;

    /// <summary>Glissement appliqué au prix d'exécution, en fraction — 5 points de base.</summary>
    public decimal SlippageRate { get; init; } = 0.0005m;

    /// <summary>Nombre de séances pendant lesquelles un ordre non exécuté est reporté.</summary>
    public int OrderValidityDays { get; init; } = 1;

    public ExitMode ExitMode { get; init; } = ExitMode.CloseAll;

    /// <summary>Fraction soldée en mode <see cref="ExitMode.Fraction"/>.</summary>
    public decimal ExitFraction { get; init; } = 1m;

    public static ExecutionPolicy Frictionless { get; } = new()
    {
        CommissionRate = 0m,
        MinimumCommission = 0m,
        SlippageRate = 0m,
    };
}
