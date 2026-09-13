using System.Collections.Concurrent;
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
}

public sealed record OptimizationOutcome(StrategyDefinition Strategy, BacktestResult Result)
{
    /// <summary>Critère de classement : le rendement annualisé rapporté à la pire baisse.</summary>
    public decimal Score => Result.Metrics.Calmar;
}

/// <summary>
/// Explore les combinaisons de règles.
/// <para>Trois défauts disparaissent ici. La taille de combinaison était figée à 4, si bien
/// qu'une seule combinaison était réellement évaluée. Le résultat vide provoquait un
/// déréférencement nul au lieu d'être une réponse légitime. Et chaque combinaison recalculait
/// l'intégralité de ses indicateurs — ici un cache unique les partage toutes.</para>
/// <para>Le classement se fait par <b>Calmar</b> et non par performance brute : maximiser le
/// gain sur une période unique est du surapprentissage assumé.</para>
/// </summary>
public sealed class StrategyOptimizer
{
    public IReadOnlyList<OptimizationOutcome> Run(OptimizationRequest request, CancellationToken cancellationToken = default)
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

        if (request.MaximumRules < request.MinimumRules)
        {
            throw new ArgumentException($"La taille maximale ({request.MaximumRules}) ne peut pas être inférieure à la minimale ({request.MinimumRules}).", nameof(request));
        }

        var upper = Math.Min(request.MaximumRules, request.Catalog.Count);

        if (request.MinimumRules > request.Catalog.Count)
        {
            // Un catalogue trop petit ne donne aucune combinaison : c'est un résultat, pas une erreur.
            return [];
        }

        var combinations = new List<int[]>();
        for (var size = request.MinimumRules; size <= upper; size++)
        {
            combinations.AddRange(Combinations(request.Catalog.Count, size));
        }

        // Un seul cache pour toutes les combinaisons : sans état après remplissage, il se lit
        // en parallèle sans verrou.
        var cache = new IndicatorCache(request.Universe);
        var outcomes = new ConcurrentBag<OptimizationOutcome>();

        void Evaluate(int[] combination)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var strategy = Compose(request, combination);
            var result = new BacktestEngine().Run(new BacktestRequest
            {
                Strategy = strategy,
                Universe = request.Universe,
                InitialCash = request.InitialCash,
                From = request.From,
                To = request.To,
                Cache = cache,
            });

            outcomes.Add(new OptimizationOutcome(strategy, result));
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

        return
        [
            .. outcomes
                .OrderByDescending(static o => o.Score)
                .ThenByDescending(static o => o.Result.Metrics.TotalReturn)
                .ThenBy(static o => o.Strategy.Fingerprint, StringComparer.Ordinal)
                .Take(request.Top),
        ];
    }

    private static StrategyDefinition Compose(OptimizationRequest request, int[] combination) => new()
    {
        Name = string.Join(" + ", combination.Select(i => request.Catalog[i].Indicator)),
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
