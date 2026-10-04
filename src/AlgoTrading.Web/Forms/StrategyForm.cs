using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Strategies.Rules;

namespace AlgoTrading.Web.Forms;

/// <summary>
/// Version modifiable d'une <see cref="StrategyDefinition"/>, liée aux champs de l'éditeur.
/// <para>Les taux s'y saisissent <b>en pour cent</b> — on tape 10 pour 10 % — et ne
/// redeviennent des fractions qu'à la conversion : la convention du domaine reste intacte.</para>
/// </summary>
public sealed class StrategyForm
{
    public string Name { get; set; } = string.Empty;

    public SignalPolicyForm Entry { get; set; } = new();

    public bool HasExit { get; set; }

    public SignalPolicyForm Exit { get; set; } = new();

    public SizingMode SizingMode { get; set; } = PositionSizing.DefaultMode;

    /// <summary>Fraction du portefeuille, ou montant en euros — la valeur telle que le domaine la lit.</summary>
    public decimal SizingValue { get; set; } = PositionSizing.DefaultValue;

    /// <summary>Ce que l'éditeur affiche : des pour cent pour une fraction, des euros sinon.</summary>
    public decimal SizingInput
    {
        get => SizingMode == SizingMode.EquityFraction ? Percent.Of(SizingValue) : SizingValue;
        set => SizingValue = SizingMode == SizingMode.EquityFraction ? Percent.Update(SizingValue, value) : value;
    }

    public decimal? StopLoss { get; set; }

    public decimal? StopLossPercent { get => Percent.Of(StopLoss); set => StopLoss = Percent.Update(StopLoss, value); }

    public decimal? TakeProfit { get; set; }

    public decimal? TakeProfitPercent { get => Percent.Of(TakeProfit); set => TakeProfit = Percent.Update(TakeProfit, value); }

    public decimal? TrailingRate { get; set; }

    public decimal? TrailingRatePercent { get => Percent.Of(TrailingRate); set => TrailingRate = Percent.Update(TrailingRate, value); }

    public bool UseTrailingAtr { get; set; }

    public decimal TrailingAtrMultiple { get; set; } = 3m;

    public int TrailingAtrPeriod { get; set; } = TrailingAtrStop.DefaultPeriod;

    public decimal CommissionRate { get; set; } = ExecutionPolicy.DefaultCommissionRate;

    public decimal CommissionPercent { get => Percent.Of(CommissionRate); set => CommissionRate = Percent.Update(CommissionRate, value); }

    public decimal MinimumCommission { get; set; } = ExecutionPolicy.DefaultMinimumCommission;

    public decimal SlippageRate { get; set; } = ExecutionPolicy.DefaultSlippageRate;

    public decimal SlippagePercent { get => Percent.Of(SlippageRate); set => SlippageRate = Percent.Update(SlippageRate, value); }

    public int OrderValidityDays { get; set; } = ExecutionPolicy.DefaultOrderValidityDays;

    public ExitMode ExitMode { get; set; } = ExitMode.CloseAll;

    public decimal ExitFraction { get; set; } = ExecutionPolicy.DefaultExitFraction;

    public decimal ExitFractionPercent { get => Percent.Of(ExitFraction); set => ExitFraction = Percent.Update(ExitFraction, value); }

    public static StrategyForm From(StrategyDefinition strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        return new StrategyForm
        {
            Name = strategy.Name,
            Entry = SignalPolicyForm.From(strategy.Entry),
            HasExit = strategy.Exit is not null,
            Exit = strategy.Exit is { } exit ? SignalPolicyForm.From(exit) : new SignalPolicyForm(),
            SizingMode = strategy.Sizing.Mode,
            SizingValue = strategy.Sizing.Value,
            StopLoss = strategy.Risk.StopLoss,
            TakeProfit = strategy.Risk.TakeProfit,
            TrailingRate = strategy.Risk.TrailingRate,
            UseTrailingAtr = strategy.Risk.TrailingAtr is not null,
            TrailingAtrMultiple = strategy.Risk.TrailingAtr?.Multiple ?? 3m,
            TrailingAtrPeriod = strategy.Risk.TrailingAtr?.Period ?? TrailingAtrStop.DefaultPeriod,
            CommissionRate = strategy.Execution.CommissionRate,
            MinimumCommission = strategy.Execution.MinimumCommission,
            SlippageRate = strategy.Execution.SlippageRate,
            OrderValidityDays = strategy.Execution.OrderValidityDays,
            ExitMode = strategy.Execution.ExitMode,
            ExitFraction = strategy.Execution.ExitFraction,
        };
    }

    /// <summary>Convertit sans valider : <see cref="StrategyDefinition.Validate"/> s'en charge.</summary>
    public StrategyDefinition ToDefinition() => new()
    {
        Name = Name.Trim(),
        Entry = Entry.ToPolicy(),
        Exit = HasExit ? Exit.ToPolicy() : null,
        Sizing = new PositionSizing(SizingMode, SizingValue),
        Risk = new RiskPolicy
        {
            StopLoss = StopLoss,
            TakeProfit = TakeProfit,
            TrailingRate = TrailingRate,
            TrailingAtr = UseTrailingAtr ? new TrailingAtrStop(TrailingAtrMultiple, TrailingAtrPeriod) : null,
        },
        Execution = new ExecutionPolicy(CommissionRate, MinimumCommission, SlippageRate, OrderValidityDays, ExitMode, ExitFraction),
    };

    /// <summary>
    /// Ce que la validation du domaine laisse passer mais qui rendrait une règle muette : une
    /// ligne que l'indicateur ne produit pas n'a jamais de valeur, donc jamais de voix.
    /// </summary>
    public IReadOnlyList<string> Problems(IndicatorLineCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var problems = new List<string>();
        Collect("Entrée", Entry);

        if (HasExit)
        {
            Collect("Sortie", Exit);
        }

        return problems;

        void Collect(string section, SignalPolicyForm policy)
        {
            for (var i = 0; i < policy.Rules.Count; i++)
            {
                foreach (var problem in policy.Rules[i].Problems(catalog))
                {
                    problems.Add($"{section}, règle {i + 1} : {problem}");
                }
            }
        }
    }
}

public sealed class SignalPolicyForm
{
    public AggregationMode Mode { get; set; } = SignalPolicy.DefaultMode;

    public decimal Threshold { get; set; } = SignalPolicy.DefaultThreshold;

    public decimal ThresholdPercent { get => Percent.Of(Threshold); set => Threshold = Percent.Update(Threshold, value); }

    public List<RuleForm> Rules { get; set; } = [];

    public static SignalPolicyForm From(SignalPolicy policy) => new()
    {
        Mode = policy.Mode,
        Threshold = policy.Threshold,
        Rules = [.. policy.Rules.Select(RuleForm.From)],
    };

    public SignalPolicy ToPolicy() => new(Mode, Threshold, [.. Rules.Select(static r => r.ToConfig())]);
}

/// <summary>Une règle en cours d'édition : tous les réglages possibles, seuls ceux de son type sont écrits.</summary>
public sealed class RuleForm
{
    public string Type { get; set; } = ThresholdRule.Type;

    public string Indicator { get; set; } = string.Empty;

    public Dictionary<string, decimal> Parameters { get; set; } = [];

    public decimal Weight { get; set; } = RuleConfig.DefaultWeight;

    public bool AsEvent { get; set; }

    public decimal BullishBelow { get; set; } = 30m;

    public decimal BearishAbove { get; set; } = 70m;

    public string Line { get; set; } = IndicatorLines.Value;

    public string FastLine { get; set; } = IndicatorLines.Fast;

    public string SlowLine { get; set; } = IndicatorLines.Slow;

    public CrossoverMode CrossoverMode { get; set; } = CrossoverMode.Cross;

    public bool MeanReverting { get; set; } = true;

    public bool BullishWhenAbove { get; set; } = true;

    public decimal Pivot { get; set; }

    /// <summary>Règle par défaut d'un indicateur, celle qui reproduit son comportement historique.</summary>
    public static RuleForm For(IndicatorInfo indicator)
    {
        ArgumentNullException.ThrowIfNull(indicator);

        var form = indicator.Kind == Domain.Indicators.Catalog.AverageTrueRange.Kind
            ? new RuleForm { Type = ThresholdRule.Type, Indicator = indicator.Kind }
            : From(RuleRegistry.DefaultFor(indicator.Kind));

        form.Parameters = indicator.Defaults.ToDictionary(static p => p.Name, static p => p.Value, StringComparer.Ordinal);
        return form;
    }

    public static RuleForm From(RuleConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new RuleForm
        {
            Type = config.Type,
            Indicator = config.Indicator,
            Parameters = new Dictionary<string, decimal>(config.Parameters, StringComparer.Ordinal),
            Weight = config.Weight,
            AsEvent = config.AsEvent,
            BullishBelow = config.BullishBelow ?? 30m,
            BearishAbove = config.BearishAbove ?? 70m,
            Line = config.Line ?? IndicatorLines.Value,
            FastLine = config.FastLine ?? IndicatorLines.Fast,
            SlowLine = config.SlowLine ?? IndicatorLines.Slow,
            CrossoverMode = config.Mode ?? CrossoverMode.Cross,
            MeanReverting = config.MeanReverting ?? true,
            BullishWhenAbove = config.BullishWhenAbove ?? true,
            Pivot = config.Pivot ?? 0m,
        };
    }

    public RuleConfig ToConfig()
    {
        var config = new RuleConfig
        {
            Type = Type,
            Indicator = Indicator,
            Parameters = new Dictionary<string, decimal>(Parameters, StringComparer.Ordinal),
            Weight = Weight,
            AsEvent = AsEvent,
        };

        // Les lignes par défaut ne s'écrivent pas : le fichier reste celui qu'on aurait tapé.
        var line = Line == IndicatorLines.Value ? null : Line;

        return Type switch
        {
            ThresholdRule.Type => config with { BullishBelow = BullishBelow, BearishAbove = BearishAbove, Line = line },
            CrossoverRule.Type => config with
            {
                FastLine = FastLine == IndicatorLines.Fast ? null : FastLine,
                SlowLine = SlowLine == IndicatorLines.Slow ? null : SlowLine,
                Mode = CrossoverMode,
            },
            BandBreakoutRule.Type => config with { MeanReverting = MeanReverting },
            PriceVsLevelRule.Type => config with { BullishWhenAbove = BullishWhenAbove, Line = line },
            SignRule.Type => config with { Pivot = Pivot, Line = line },
            _ => config,
        };
    }

    /// <summary>Change d'indicateur : ses paramètres par défaut, et des lignes qu'il produit vraiment.</summary>
    public void ChangeIndicator(IndicatorInfo indicator)
    {
        ArgumentNullException.ThrowIfNull(indicator);

        Indicator = indicator.Kind;
        Parameters = indicator.Defaults.ToDictionary(static p => p.Name, static p => p.Value, StringComparer.Ordinal);

        Line = FastLine = SlowLine = string.Empty;
        FitLines(indicator);
    }

    /// <summary>
    /// Remplace les lignes que l'indicateur ne produit pas et garde les autres. À défaut de
    /// lignes rapide et lente, un croisement lit la ligne principale contre sa ligne de signal.
    /// </summary>
    private void FitLines(IndicatorInfo indicator)
    {
        var lines = indicator.Lines;

        if (!lines.Contains(Line))
        {
            Line = First(lines, IndicatorLines.Value) ?? lines[0];
        }

        if (!lines.Contains(FastLine))
        {
            FastLine = First(lines, IndicatorLines.Fast, IndicatorLines.Value) ?? lines[0];
        }

        if (!lines.Contains(SlowLine) || SlowLine == FastLine)
        {
            SlowLine = First(lines.Where(l => l != FastLine).ToList(), IndicatorLines.Slow, IndicatorLines.Signal)
                ?? lines.FirstOrDefault(l => l != FastLine)
                ?? lines[0];
        }
    }

    private static string? First(IReadOnlyList<string> lines, params string[] preferred) =>
        preferred.FirstOrDefault(lines.Contains);

    /// <summary>Change de type ; l'indicateur est conservé s'il convient, remplacé sinon.</summary>
    public void ChangeType(string type, IndicatorLineCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        Type = type;

        if (catalog.Find(Indicator) is not { } current || !catalog.Supports(type, current))
        {
            ChangeIndicator(catalog.CompatibleWith(type).First());
        }
        else
        {
            FitLines(current);
        }
    }

    public IEnumerable<string> Problems(IndicatorLineCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.Find(Indicator) is not { } indicator)
        {
            yield return $"indicateur inconnu « {Indicator} ».";
            yield break;
        }

        if (!catalog.Supports(Type, indicator))
        {
            yield return $"{Indicator} ne fournit pas les lignes qu'attend une règle « {Labels.RuleType(Type)} ».";
        }

        IEnumerable<string> used = Type switch
        {
            ThresholdRule.Type or PriceVsLevelRule.Type or SignRule.Type => [Line],
            CrossoverRule.Type => [FastLine, SlowLine],
            _ => [],
        };

        foreach (var line in used.Where(l => !indicator.Lines.Contains(l)))
        {
            yield return $"{Indicator} ne produit pas de ligne « {line} » : la règle ne voterait jamais.";
        }
    }
}

/// <summary>
/// Le pour cent n'est qu'une vue sur une fraction.
/// <para>La fraction d'origine est conservée tant que la valeur saisie ne change pas : la
/// forme canonique d'une stratégie écrit les décimaux tels quels, et 0,0010 rendu en 0,001
/// changerait son empreinte — un run déjà enregistré cesserait d'être reconnu comme le même.</para>
/// </summary>
internal static class Percent
{
    public static decimal Of(decimal fraction) => Normalize(fraction * 100m);

    public static decimal? Of(decimal? fraction) => fraction is { } value ? Of(value) : null;

    public static decimal Update(decimal fraction, decimal percent) =>
        Of(fraction) == percent ? fraction : Normalize(percent / 100m);

    public static decimal? Update(decimal? fraction, decimal? percent) => percent switch
    {
        null => null,
        { } value when fraction is { } current => Update(current, value),
        { } value => Normalize(value / 100m),
    };

    /// <summary>Retire les zéros de fin : <c>0.100m</c> devient <c>0.1m</c>.</summary>
    private static decimal Normalize(decimal value) => value / 1.0000000000000000000000000000m;
}
