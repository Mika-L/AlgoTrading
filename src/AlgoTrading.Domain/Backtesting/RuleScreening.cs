using AlgoTrading.Domain.Reporting;
using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Domain.Backtesting;

/// <summary>Une variante jouée seule, et ce qu'en disent ses voisines.</summary>
/// <param name="Eligible">A atteint le nombre minimal de trades.</param>
/// <param name="Neighbours">Variantes voisines : même règle, un seul réglage décalé d'un cran
/// dans la grille.</param>
/// <param name="NeighbourhoodScore">Calmar moyen de la variante et de ses voisines, une variante
/// non éligible comptant pour zéro. Un plateau le garde haut, un pic isolé le fait chuter.</param>
/// <param name="WorstNeighbour">Le plus faible Calmar du voisinage, compté de même.</param>
public sealed record ScreenedVariant(
    RuleConfig Rule,
    string Label,
    PerformanceMetrics Metrics,
    bool Eligible,
    int Neighbours,
    decimal NeighbourhoodScore,
    decimal? WorstNeighbour)
{
    public decimal Score => Metrics.Calmar;
}

/// <summary>Les variantes d'un indicateur, de la plus robuste à la moins robuste.</summary>
public sealed record ScreeningGroup(string Indicator, IReadOnlyList<ScreenedVariant> Variants);

public sealed record ScreeningReport(IReadOnlyList<ScreeningGroup> Groups, long Evaluated);

/// <summary>
/// Premier étage de l'entonnoir : chaque variante du catalogue est jouée seule, puis jugée avec
/// ses voisines de grille.
/// <para>Une variante dont les voisines réussissent aussi a trouvé un comportement du marché ;
/// une variante brillante entourée de médiocres a trouvé une coïncidence de sa période. Le
/// classement se fait donc sur le voisinage, pas sur la variante seule.</para>
/// </summary>
public sealed class RuleScreening
{
    public ScreeningReport Run(
        OptimizationRequest request,
        IProgress<OptimizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Toutes les variantes, éligibles ou non : le voisinage a besoin de chacune.
        var report = new StrategyOptimizer().Run(
            request with
            {
                MinimumRules = 1,
                MaximumRules = 1,
                Top = request.Catalog.Count,
                MinimumTrades = 0,
                SampleSize = null,
                OneVariantPerIndicator = false,
            },
            progress,
            cancellationToken);

        // Les règles des stratégies composées sont celles du catalogue, à l'identique.
        var metrics = new Dictionary<RuleConfig, PerformanceMetrics>(ReferenceEqualityComparer.Instance);
        foreach (var candidate in report.Top)
        {
            metrics[candidate.Strategy.Entry.Rules[0]] = candidate.Metrics;
        }

        var points = request.Catalog
            .Select(rule => new Point(rule, metrics[rule], metrics[rule].TradeCount >= request.MinimumTrades))
            .ToArray();

        var screened = points
            .GroupBy(static p => p.Family, StringComparer.Ordinal)
            .SelectMany(static family => Neighbourhoods([.. family]))
            .ToArray();

        var groups = screened
            .GroupBy(static v => v.Rule.Indicator, StringComparer.Ordinal)
            .Select(static group => new ScreeningGroup(
                group.Key,
                [
                    .. group
                        .OrderByDescending(static v => v.NeighbourhoodScore)
                        .ThenByDescending(static v => v.Score)
                        .ThenBy(static v => v.Label, StringComparer.Ordinal),
                ]))
            .OrderByDescending(static g => g.Variants[0].NeighbourhoodScore)
            .ThenBy(static g => g.Indicator, StringComparer.Ordinal)
            .ToArray();

        return new ScreeningReport(groups, report.Evaluated);
    }

    /// <summary>
    /// Dans une famille, deux variantes sont voisines quand elles ne diffèrent que d'un réglage,
    /// d'un seul cran dans les valeurs que la grille donne à ce réglage.
    /// </summary>
    private static IEnumerable<ScreenedVariant> Neighbourhoods(Point[] family)
    {
        var steps = family[0].Axes.Keys.ToDictionary(
            static key => key,
            key => family.Select(p => p.Axes[key]).Distinct().Order().ToList(),
            StringComparer.Ordinal);

        int[] Position(Point point) => [.. point.Axes.Select(axis => steps[axis.Key].IndexOf(axis.Value))];

        var positions = family.Select(Position).ToArray();

        for (var i = 0; i < family.Length; i++)
        {
            var neighbours = new List<decimal>();

            for (var j = 0; j < family.Length; j++)
            {
                var gaps = positions[i].Zip(positions[j], static (a, b) => Math.Abs(a - b)).ToArray();

                if (gaps.Count(static gap => gap != 0) == 1 && gaps.Sum() == 1)
                {
                    neighbours.Add(family[j].Evidence);
                }
            }

            yield return new ScreenedVariant(
                family[i].Rule,
                StrategyOptimizer.Label(family[i].Rule),
                family[i].Metrics,
                family[i].Eligible,
                neighbours.Count,
                (family[i].Evidence + neighbours.Sum()) / (neighbours.Count + 1),
                neighbours.Count == 0 ? null : neighbours.Min());
        }
    }

    private sealed class Point(RuleConfig rule, PerformanceMetrics metrics, bool eligible)
    {
        public RuleConfig Rule { get; } = rule;

        public PerformanceMetrics Metrics { get; } = metrics;

        public bool Eligible { get; } = eligible;

        /// <summary>Ce que la variante prouve : son Calmar si elle a assez tradé, rien sinon.</summary>
        public decimal Evidence => Eligible ? Metrics.Calmar : 0m;

        /// <summary>Les réglages numériques, seuls susceptibles de former une grille.</summary>
        public SortedDictionary<string, decimal> Axes { get; } = AxesOf(rule);

        /// <summary>
        /// Tout ce qui n'est pas un axe numérique : deux variantes de familles différentes ne
        /// sont jamais voisines, quelle que soit la proximité de leurs nombres.
        /// </summary>
        public string Family { get; } = string.Join(
            '|',
            rule.Type,
            rule.Indicator,
            rule.AsEvent,
            rule.Line,
            rule.FastLine,
            rule.SlowLine,
            rule.Mode,
            rule.MeanReverting,
            rule.BullishWhenAbove,
            string.Join(',', AxesOf(rule).Keys));

        private static SortedDictionary<string, decimal> AxesOf(RuleConfig rule)
        {
            var axes = new SortedDictionary<string, decimal>(StringComparer.Ordinal);

            foreach (var (name, value) in rule.Parameters)
            {
                axes[$"parameters.{name}"] = value;
            }

            void Add(string name, decimal? value)
            {
                if (value is { } present)
                {
                    axes[name] = present;
                }
            }

            Add(nameof(RuleConfig.BullishBelow), rule.BullishBelow);
            Add(nameof(RuleConfig.BearishAbove), rule.BearishAbove);
            Add(nameof(RuleConfig.Pivot), rule.Pivot);
            Add(nameof(RuleConfig.Weight), rule.Weight);

            return axes;
        }
    }
}
