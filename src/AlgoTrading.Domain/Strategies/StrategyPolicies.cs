using System.Text.Json.Serialization;
using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.Indicators.Catalog;

namespace AlgoTrading.Domain.Strategies;

/// <summary>Politique d'agrégation : les règles et la façon de les faire voter.</summary>
public sealed record SignalPolicy
{
    public const AggregationMode DefaultMode = AggregationMode.Majority;
    public const decimal DefaultThreshold = 0.5m;

    public SignalPolicy()
    {
    }

    /// <summary>Voir <see cref="RuleConfig"/> : les défauts doivent être portés par le constructeur.</summary>
    [JsonConstructor]
    public SignalPolicy(AggregationMode mode = DefaultMode, decimal threshold = DefaultThreshold, IReadOnlyList<RuleConfig>? rules = null)
    {
        Mode = mode;
        Threshold = threshold;
        Rules = rules ?? [];
    }

    public AggregationMode Mode { get; init; } = DefaultMode;

    /// <summary>Part des voix à dépasser, <b>strictement</b>. Fraction entre 0 et 1.</summary>
    public decimal Threshold { get; init; } = DefaultThreshold;

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
    public const SizingMode DefaultMode = SizingMode.EquityFraction;
    public const decimal DefaultValue = 0.1m;

    public PositionSizing()
    {
    }

    [JsonConstructor]
    public PositionSizing(SizingMode mode = DefaultMode, decimal value = DefaultValue)
    {
        Mode = mode;
        Value = value;
    }

    public SizingMode Mode { get; init; } = DefaultMode;

    public decimal Value { get; init; } = DefaultValue;
}

/// <summary>
/// Stop suiveur à <see cref="Multiple"/> fois l'ATR sous le plus haut atteint depuis l'entrée.
/// <para>Là où le stop fixe se mesure au prix de revient et ne bouge jamais, celui-ci monte
/// avec le titre et ne redescend pas : c'est un cliquet, pas une bande. Une volatilité qui
/// s'élargit le laisse sur place plutôt que de le faire reculer.</para>
/// <para>La distance s'exprime en multiples d'ATR et non en pourcentage : elle se règle donc
/// sur l'amplitude propre du titre, et non sur une tolérance uniforme qui serre trop les
/// valeurs agitées et laisse filer les valeurs calmes.</para>
/// </summary>
public sealed record TrailingAtrStop
{
    public const int DefaultPeriod = 14;

    public TrailingAtrStop()
    {
    }

    /// <summary>Voir <see cref="RuleConfig"/> : sans ce constructeur, une période absente du
    /// JSON vaudrait zéro et la validation rejetterait un fichier pourtant légitime.</summary>
    [JsonConstructor]
    public TrailingAtrStop(decimal multiple, int period = DefaultPeriod)
    {
        Multiple = multiple;
        Period = period;
    }

    /// <summary>Distance au plus haut, en multiples d'ATR — trois est l'usage courant.</summary>
    public decimal Multiple { get; init; }

    public int Period { get; init; } = DefaultPeriod;

    /// <summary>
    /// Identité du calcul d'ATR dont le moteur a besoin. Dérivée, donc jamais sérialisée :
    /// un fichier de stratégie décrit une intention, pas le plan de calcul qui en découle.
    /// </summary>
    [JsonIgnore]
    public IndicatorDescriptor Atr => IndicatorDescriptor.Of(AverageTrueRange.Kind, ("period", Period));
}

/// <summary>
/// Stop de protection, prise de bénéfice et stops suiveurs.
/// <para>Tout ce qui se compte en <c>Rate</c> ou en fraction se lit de la même façon dans ce
/// projet : <c>0,02</c> vaut 2 %. <see cref="StopLoss"/> et <see cref="TakeProfit"/> se
/// mesurent au prix de revient, <see cref="TrailingRate"/> au plus haut atteint depuis
/// l'entrée, <see cref="TrailingAtr"/> à ce même plus haut mais en multiples d'ATR.</para>
/// <para>Livrés et testés mais <b>inactifs par défaut</b> : décision arbitrée, pour que le
/// premier backtest après refactor reste comparable à l'ancien.</para>
/// <para>Rien n'interdit de les cumuler : le moteur retient à chaque séance le plus
/// protecteur des stops configurés.</para>
/// </summary>
public sealed record RiskPolicy
{
    public decimal? StopLoss { get; init; }

    public decimal? TakeProfit { get; init; }

    /// <summary>
    /// Stop suiveur à distance constante : <c>0,10</c> le tient dix pour cent sous le plus
    /// haut atteint depuis l'entrée. La tolérance est la même quel que soit le titre, agité
    /// ou tranquille — c'est sa force et sa faiblesse, là où <see cref="TrailingAtr"/> se
    /// règle sur l'amplitude propre de chacun.
    /// </summary>
    public decimal? TrailingRate { get; init; }

    /// <summary>Absent, aucun stop suiveur en multiples d'ATR.</summary>
    public TrailingAtrStop? TrailingAtr { get; init; }

    public bool IsActive => StopLoss.HasValue || TakeProfit.HasValue || HasTrailingStop;

    public bool HasTrailingStop => TrailingRate.HasValue || TrailingAtr is not null;

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
    public const decimal DefaultCommissionRate = 0.0010m;
    public const decimal DefaultMinimumCommission = 1m;
    public const decimal DefaultSlippageRate = 0.0005m;
    public const int DefaultOrderValidityDays = 1;
    public const decimal DefaultExitFraction = 1m;

    public ExecutionPolicy()
    {
    }

    /// <summary>
    /// Voir <see cref="RuleConfig"/>. C'est ici que l'enjeu est le plus net : sans ce
    /// constructeur, une stratégie qui ne mentionne pas ses frais serait silencieusement
    /// backtestée à commission et glissement nuls.
    /// </summary>
    [JsonConstructor]
    public ExecutionPolicy(
        decimal commissionRate = DefaultCommissionRate,
        decimal minimumCommission = DefaultMinimumCommission,
        decimal slippageRate = DefaultSlippageRate,
        int orderValidityDays = DefaultOrderValidityDays,
        ExitMode exitMode = ExitMode.CloseAll,
        decimal exitFraction = DefaultExitFraction)
    {
        CommissionRate = commissionRate;
        MinimumCommission = minimumCommission;
        SlippageRate = slippageRate;
        OrderValidityDays = orderValidityDays;
        ExitMode = exitMode;
        ExitFraction = exitFraction;
    }

    /// <summary>Commission proportionnelle, en fraction — 10 points de base par défaut.</summary>
    public decimal CommissionRate { get; init; } = DefaultCommissionRate;

    public decimal MinimumCommission { get; init; } = DefaultMinimumCommission;

    /// <summary>Glissement appliqué au prix d'exécution, en fraction — 5 points de base.</summary>
    public decimal SlippageRate { get; init; } = DefaultSlippageRate;

    /// <summary>Nombre de séances pendant lesquelles un ordre non exécuté est reporté.</summary>
    public int OrderValidityDays { get; init; } = DefaultOrderValidityDays;

    public ExitMode ExitMode { get; init; } = ExitMode.CloseAll;

    /// <summary>Fraction soldée en mode <see cref="ExitMode.Fraction"/>.</summary>
    public decimal ExitFraction { get; init; } = DefaultExitFraction;

    public static ExecutionPolicy Frictionless { get; } = new()
    {
        CommissionRate = 0m,
        MinimumCommission = 0m,
        SlippageRate = 0m,
    };
}
