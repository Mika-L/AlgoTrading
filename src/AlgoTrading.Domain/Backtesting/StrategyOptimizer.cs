using System.Globalization;
using System.Numerics;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Reporting;
using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Domain.Backtesting;

public sealed record OptimizationRequest
{
    /// <summary>Règles candidates, parmi lesquelles les combinaisons sont tirées.</summary>
    public required IReadOnlyList<RuleConfig> Catalog { get; init; }

    public required IReadOnlyList<BarSeries> Universe { get; init; }

    public int MinimumRules { get; init; } = 2;

    public int MaximumRules { get; init; } = 4;

    public decimal InitialCash { get; init; } = 100_000m;

    /// <summary>
    /// Bornes de la fenêtre testée. Présentes dès maintenant pour qu'un walk-forward futur
    /// soit une boucle sur des plages de dates, et non une refonte.
    /// </summary>
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public AggregationMode Mode { get; init; } = AggregationMode.Majority;

    public decimal Threshold { get; init; } = 0.5m;

    public PositionSizing Sizing { get; init; } = new();

    public RiskPolicy Risk { get; init; } = RiskPolicy.None;

    public ExecutionPolicy Execution { get; init; } = new();

    public int Top { get; init; } = 20;

    public bool Parallel { get; init; } = true;

    /// <summary>
    /// En deçà, une combinaison est évaluée et comptée, mais pas classée : quatre trades en
    /// neuf ans donnent un Calmar spectaculaire qui ne prouve que la chance.
    /// </summary>
    public int MinimumTrades { get; init; }

    /// <summary>
    /// Nombre de combinaisons à tirer au hasard, sans remise, dans l'espace de recherche.
    /// Absent, ou supérieur à l'espace, l'exploration est exhaustive.
    /// </summary>
    public long? SampleSize { get; init; }

    /// <summary>Graine du tirage : même graine, mêmes combinaisons.</summary>
    public ulong Seed { get; init; } = 1;

    /// <summary>
    /// Interdit de combiner deux variantes d'un même indicateur — <c>Rsi(7)</c> avec
    /// <c>Rsi(14)</c> vote deux fois la même idée. Sans effet sur un catalogue à une variante
    /// par indicateur.
    /// </summary>
    public bool OneVariantPerIndicator { get; init; } = true;

    /// <summary>
    /// Cache d'indicateurs à réutiliser d'une exploration à l'autre sur le même univers — d'une
    /// fenêtre de walk-forward à la suivante, par exemple. Absent, chaque exploration a le sien.
    /// </summary>
    public IndicatorCache? Cache { get; init; }
}

/// <summary>
/// Une combinaison classée. Elle ne garde que ses mesures : conserver courbe et trades de
/// chaque run saturait la mémoire au-delà de quelques dizaines de milliers d'essais. Le détail
/// se retrouve en rejouant la stratégie.
/// </summary>
public sealed record OptimizationCandidate(StrategyDefinition Strategy, PerformanceMetrics Metrics)
{
    /// <summary>Critère de classement : le rendement annualisé rapporté à la pire baisse.</summary>
    public decimal Score => Metrics.Calmar;
}

public sealed record OptimizationProgress(long Evaluated, long Planned, long Eligible, OptimizationCandidate? Best);

/// <param name="Top">Les meilleures combinaisons éligibles, de la meilleure à la moins bonne.</param>
/// <param name="SearchSpace">Nombre de combinaisons possibles avec ce catalogue et ces bornes.</param>
/// <param name="Evaluated">Combinaisons réellement essayées. C'est ce nombre, et non la taille
/// du classement, qui mesure le risque de découverte fortuite.</param>
/// <param name="Eligible">Combinaisons essayées ayant atteint le nombre minimal de trades.</param>
public sealed record OptimizationReport(IReadOnlyList<OptimizationCandidate> Top, BigInteger SearchSpace, long Evaluated, long Eligible)
{
    public static OptimizationReport Empty { get; } = new([], BigInteger.Zero, 0, 0);
}

/// <summary>
/// Explore les combinaisons de règles <b>en flux</b> : elles sont générées à la volée, chaque
/// résultat est réduit à ses mesures dès sa sortie du moteur, et seul un classement borné est
/// tenu à jour. La mémoire ne dépend plus du nombre d'essais, seulement de <c>Top</c>.
/// <para>Un cache unique d'indicateurs sert toutes les combinaisons : chaque variante n'est
/// calculée qu'une fois par titre.</para>
/// <para>Le classement se fait par <b>Calmar</b> et non par performance brute : maximiser le
/// gain sur une période unique est du surapprentissage assumé.</para>
/// </summary>
public sealed class StrategyOptimizer
{
    public OptimizationReport Run(
        OptimizationRequest request,
        IProgress<OptimizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Catalog.Count == 0)
        {
            throw new ArgumentException("L'optimisation a besoin d'un catalogue de règles.", nameof(request));
        }

        if (request.Universe.Count == 0)
        {
            throw new ArgumentException("L'optimisation a besoin d'au moins un titre.", nameof(request));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(request.MinimumRules, 1, nameof(request.MinimumRules));
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Top, 1, nameof(request.Top));
        ArgumentOutOfRangeException.ThrowIfNegative(request.MinimumTrades, nameof(request.MinimumTrades));

        if (request.SampleSize is { } requested)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(requested, 1, nameof(request.SampleSize));
        }

        if (request.MaximumRules < request.MinimumRules)
        {
            throw new ArgumentException($"La taille maximale ({request.MaximumRules}) ne peut pas être inférieure à la minimale ({request.MinimumRules}).", nameof(request));
        }

        var space = CombinationSpace.Of(request);

        if (space.Total.IsZero)
        {
            // Un catalogue trop petit ne donne aucune combinaison : c'est un résultat, pas une erreur.
            return OptimizationReport.Empty;
        }

        var exhaustive = request.SampleSize is not { } sample || sample >= space.Total;

        if (exhaustive && space.Total > long.MaxValue)
        {
            throw new ArgumentException($"{space.Total} combinaisons ne s'énumèrent pas : demandez un échantillon.", nameof(request));
        }

        var planned = exhaustive ? (long)space.Total : request.SampleSize!.Value;
        var combinations = exhaustive ? space.Enumerate() : space.Sample(planned, new SeededRandom(request.Seed));

        // Un seul cache pour toutes les combinaisons : sans état après remplissage, il se lit
        // en parallèle sans verrou.
        var cache = request.Cache ?? new IndicatorCache(request.Universe);
        var ranking = new BoundedRanking(request.Top);
        var reportEvery = Math.Max(1, planned / 200);
        long evaluated = 0;
        long eligible = 0;

        void Evaluate(int[] combination)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var strategy = Compose(request, combination);
            var metrics = new BacktestEngine().Run(new BacktestRequest
            {
                Strategy = strategy,
                Universe = request.Universe,
                InitialCash = request.InitialCash,
                From = request.From,
                To = request.To,
                Cache = cache,
            }).Metrics;

            if (metrics.TradeCount >= request.MinimumTrades)
            {
                Interlocked.Increment(ref eligible);
                ranking.Offer(new OptimizationCandidate(strategy, metrics));
            }

            var done = Interlocked.Increment(ref evaluated);

            if (progress is not null && (done % reportEvery == 0 || done == planned))
            {
                progress.Report(new OptimizationProgress(done, planned, Interlocked.Read(ref eligible), ranking.Best));
            }
        }

        if (request.Parallel)
        {
            System.Threading.Tasks.Parallel.ForEach(
                combinations,
                new ParallelOptions { CancellationToken = cancellationToken },
                Evaluate);
        }
        else
        {
            foreach (var combination in combinations)
            {
                Evaluate(combination);
            }
        }

        return new OptimizationReport(ranking.Ranked(), space.Total, evaluated, eligible);
    }

    private static StrategyDefinition Compose(OptimizationRequest request, int[] combination) => new()
    {
        Name = string.Join(" + ", combination.Select(i => Label(request.Catalog[i]))),
        Entry = new SignalPolicy
        {
            Mode = request.Mode,
            Threshold = request.Threshold,
            Rules = [.. combination.Select(i => request.Catalog[i])],
        },
        Sizing = request.Sizing,
        Risk = request.Risk,
        Execution = request.Execution,
    };

    /// <summary>
    /// Nom lisible d'une règle, assez précis pour distinguer deux variantes :
    /// <c>Rsi(14) 30/70</c>, <c>Ema(9,20)</c>.
    /// </summary>
    private static string Label(RuleConfig rule)
    {
        var name = rule.Parameters.Count == 0
            ? rule.Indicator
            : $"{rule.Indicator}({string.Join(',', rule.Parameters.Values.Select(Invariant))})";

        return rule.BullishBelow is { } low && rule.BearishAbove is { } high
            ? $"{name} {Invariant(low)}/{Invariant(high)}"
            : name;
    }

    private static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Meilleur d'abord : Calmar, puis rendement, puis empreinte pour départager sans dépendre de l'ordre d'arrivée.</summary>
    private static int Compare(OptimizationCandidate left, OptimizationCandidate right)
    {
        var byScore = right.Score.CompareTo(left.Score);
        if (byScore != 0)
        {
            return byScore;
        }

        var byReturn = right.Metrics.TotalReturn.CompareTo(left.Metrics.TotalReturn);
        return byReturn != 0
            ? byReturn
            : string.CompareOrdinal(left.Strategy.Fingerprint, right.Strategy.Fingerprint);
    }

    /// <summary>
    /// Les <c>capacity</c> meilleures combinaisons vues jusqu'ici, dans un tas dont la racine est
    /// la moins bonne : une nouvelle venue ne coûte qu'une comparaison quand elle ne rentre pas.
    /// </summary>
    private sealed class BoundedRanking(int capacity)
    {
        private readonly PriorityQueue<OptimizationCandidate, OptimizationCandidate> _heap =
            new(Comparer<OptimizationCandidate>.Create(static (left, right) => Compare(right, left)));

        private readonly Lock _gate = new();

        public OptimizationCandidate? Best { get; private set; }

        public void Offer(OptimizationCandidate candidate)
        {
            lock (_gate)
            {
                if (_heap.Count < capacity)
                {
                    _heap.Enqueue(candidate, candidate);
                }
                else if (Compare(candidate, _heap.Peek()) < 0)
                {
                    _heap.EnqueueDequeue(candidate, candidate);
                }
                else
                {
                    return;
                }

                if (Best is null || Compare(candidate, Best) < 0)
                {
                    Best = candidate;
                }
            }
        }

        public IReadOnlyList<OptimizationCandidate> Ranked()
        {
            lock (_gate)
            {
                return [.. _heap.UnorderedItems.Select(static item => item.Element).Order(Comparer<OptimizationCandidate>.Create(Compare))];
            }
        }
    }

    /// <summary>
    /// L'espace de recherche : les combinaisons de <c>MinimumRules</c> à <c>MaximumRules</c>
    /// règles, à au plus une règle par groupe. Un groupe rassemble les variantes d'un même
    /// indicateur, ou une règle seule si les variantes peuvent se cumuler.
    /// </summary>
    private sealed class CombinationSpace
    {
        private readonly int[][] _groups;
        private readonly int _minimum;
        private readonly int _maximum;

        /// <summary>
        /// <c>_ways[i, k]</c> : nombre de façons de prendre <c>k</c> règles dans les groupes
        /// <c>i</c> et suivants, une au plus par groupe. Sert au dénombrement exact et au tirage
        /// uniforme sans rejet.
        /// </summary>
        private readonly BigInteger[,] _ways;

        private CombinationSpace(int[][] groups, int minimum, int maximum)
        {
            _groups = groups;
            _minimum = minimum;
            _maximum = maximum;
            _ways = new BigInteger[groups.Length + 1, maximum + 1];
            _ways[groups.Length, 0] = BigInteger.One;

            for (var i = groups.Length - 1; i >= 0; i--)
            {
                _ways[i, 0] = BigInteger.One;

                for (var k = 1; k <= maximum; k++)
                {
                    _ways[i, k] = _ways[i + 1, k] + (groups[i].Length * _ways[i + 1, k - 1]);
                }
            }

            for (var k = minimum; k <= maximum; k++)
            {
                Total += _ways[0, k];
            }
        }

        public BigInteger Total { get; }

        public static CombinationSpace Of(OptimizationRequest request)
        {
            var groups = request.OneVariantPerIndicator
                ? request.Catalog
                    .Select(static (rule, index) => (rule.Indicator, index))
                    .GroupBy(static entry => entry.Indicator, StringComparer.Ordinal)
                    .Select(static group => group.Select(static entry => entry.index).ToArray())
                    .ToArray()
                : [.. Enumerable.Range(0, request.Catalog.Count).Select(static index => new[] { index })];

            return new CombinationSpace(groups, request.MinimumRules, Math.Min(request.MaximumRules, groups.Length));
        }

        /// <summary>Chaque combinaison une fois : pour chaque choix de groupes, le produit de leurs variantes.</summary>
        public IEnumerable<int[]> Enumerate()
        {
            for (var size = _minimum; size <= _maximum; size++)
            {
                foreach (var chosen in Combinations(_groups.Length, size))
                {
                    var choice = new int[size];

                    while (true)
                    {
                        yield return [.. chosen.Select((group, slot) => _groups[group][choice[slot]])];

                        var position = size - 1;
                        while (position >= 0 && ++choice[position] == _groups[chosen[position]].Length)
                        {
                            choice[position] = 0;
                            position--;
                        }

                        if (position < 0)
                        {
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// <paramref name="count"/> combinaisons distinctes, chacune uniforme sur l'espace.
        /// <para>La taille est tirée au prorata du nombre de combinaisons qu'elle offre, puis
        /// chaque groupe est retenu avec la probabilité exacte qu'il figure dans une
        /// combinaison de la taille restante : aucune n'est favorisée, aucune n'est rejetée,
        /// hormis les doublons d'un tirage sans remise.</para>
        /// </summary>
        public IEnumerable<int[]> Sample(long count, SeededRandom random)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var total = (double)Total;

            while (seen.Count < count)
            {
                var size = _maximum;
                var draw = random.NextDouble() * total;

                for (var k = _minimum; k <= _maximum; k++)
                {
                    draw -= (double)_ways[0, k];
                    if (draw < 0)
                    {
                        size = k;
                        break;
                    }
                }

                var combination = new List<int>(size);
                var remaining = size;

                for (var group = 0; group < _groups.Length && remaining > 0; group++)
                {
                    var keep = (double)(_groups[group].Length * _ways[group + 1, remaining - 1]) / (double)_ways[group, remaining];

                    if (random.NextDouble() < keep)
                    {
                        combination.Add(_groups[group][random.Next(_groups[group].Length)]);
                        remaining--;
                    }
                }

                if (seen.Add(string.Join(',', combination)))
                {
                    yield return [.. combination];
                }
            }
        }

        /// <summary>Toutes les combinaisons de <paramref name="size"/> indices parmi <paramref name="count"/>.</summary>
        private static IEnumerable<int[]> Combinations(int count, int size)
        {
            var indices = new int[size];

            for (var i = 0; i < size; i++)
            {
                indices[i] = i;
            }

            while (true)
            {
                yield return [.. indices];

                var position = size - 1;
                while (position >= 0 && indices[position] == count - size + position)
                {
                    position--;
                }

                if (position < 0)
                {
                    yield break;
                }

                indices[position]++;
                for (var i = position + 1; i < size; i++)
                {
                    indices[i] = indices[i - 1] + 1;
                }
            }
        }
    }
}
