using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies.Rules;

namespace AlgoTrading.Web.Forms;

/// <summary>Ce qu'un formulaire doit savoir d'un indicateur : ses paramètres et les lignes qu'il produit.</summary>
public sealed record IndicatorInfo(string Kind, IReadOnlyList<IndicatorParameter> Defaults, IReadOnlyList<string> Lines)
{
    public bool HasLines(params string[] names) => names.All(Lines.Contains);
}

/// <summary>
/// Catalogue des indicateurs tel que l'éditeur le présente.
/// <para>Les lignes ne sont déclarées nulle part : chaque indicateur les nomme au calcul. On
/// les découvre donc en calculant une fois chaque indicateur sur une série synthétique, ce
/// qui reste juste quand le catalogue évolue — une liste recopiée ici ne le resterait pas.</para>
/// </summary>
public sealed class IndicatorLineCatalog
{
    private readonly Dictionary<string, IndicatorInfo> _byKind;

    private IndicatorLineCatalog(IReadOnlyList<IndicatorInfo> indicators)
    {
        Indicators = indicators;
        _byKind = indicators.ToDictionary(static i => i.Kind, StringComparer.Ordinal);
    }

    public IReadOnlyList<IndicatorInfo> Indicators { get; }

    public IndicatorInfo? Find(string kind) => _byKind.GetValueOrDefault(kind);

    /// <summary>
    /// Indicateurs qu'une règle sait lire : une sortie de bande demande deux bandes, un
    /// croisement deux lignes, le nuage les trois lignes d'Ichimoku.
    /// </summary>
    public bool Supports(string ruleType, IndicatorInfo indicator) => ruleType switch
    {
        BandBreakoutRule.Type => indicator.HasLines(IndicatorLines.Upper, IndicatorLines.Lower),
        CrossoverRule.Type => indicator.Lines.Count >= 2,
        IchimokuCloudRule.Type => indicator.HasLines(IndicatorLines.KijunSen, IndicatorLines.SenkouSpanA, IndicatorLines.SenkouSpanB),
        _ => true,
    };

    public IEnumerable<IndicatorInfo> CompatibleWith(string ruleType) => Indicators.Where(i => Supports(ruleType, i));

    public static IndicatorLineCatalog Discover()
    {
        var series = SyntheticSeries(300);

        return new IndicatorLineCatalog(
        [
            .. IndicatorCatalog.Defaults
                .Select(i => new IndicatorInfo(i.Descriptor.Kind, i.Descriptor.Parameters, i.Compute(series).LineNames))
                .OrderBy(static i => i.Kind, StringComparer.Ordinal),
        ]);
    }

    /// <summary>Une oscillation régulière, assez longue pour amorcer tous les indicateurs.</summary>
    private static BarSeries SyntheticSeries(int count)
    {
        var date = new DateOnly(2020, 1, 6);
        var bars = new List<PriceBar>(count);

        for (var i = 0; i < count; i++)
        {
            var close = 100m + (decimal)(10 * Math.Sin(i / 7.0));
            bars.Add(new PriceBar(date, close, close + 1m, close - 1m, close, 1_000, close));
            date = date.DayOfWeek == DayOfWeek.Friday ? date.AddDays(3) : date.AddDays(1);
        }

        return BarSeries.Create(Symbol.From("SYNTH"), bars);
    }
}
