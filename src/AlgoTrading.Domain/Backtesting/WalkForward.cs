using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Reporting;

namespace AlgoTrading.Domain.Backtesting;

public sealed record WalkForwardRequest
{
    /// <summary>
    /// Réglages de l'exploration menée sur chaque fenêtre d'apprentissage. Ses bornes de dates
    /// sont ignorées : ce sont les fenêtres qui les fixent.
    /// </summary>
    public required OptimizationRequest Optimization { get; init; }

    /// <summary>Début de la première fenêtre ; à défaut, la première séance de l'univers.</summary>
    public DateOnly? From { get; init; }

    /// <summary>
    /// Fin de la dernière fenêtre ; à défaut, la dernière séance de l'univers. Une période
    /// réservée au verdict final se protège en arrêtant les fenêtres avant elle.
    /// </summary>
    public DateOnly? To { get; init; }

    public int TrainingMonths { get; init; } = 36;

    /// <summary>Durée de chaque fenêtre de test, et pas du décalage d'une fenêtre à la suivante.</summary>
    public int TestMonths { get; init; } = 12;
}

/// <param name="Training">L'exploration menée sur la fenêtre d'apprentissage.</param>
/// <param name="Selected">La meilleure combinaison de l'apprentissage, ou rien si aucune n'a
/// atteint le nombre minimal de trades — la fenêtre de test se passe alors en liquidités.</param>
/// <param name="Test">La combinaison retenue jouée sur la fenêtre de test, qu'elle n'a jamais vue.</param>
/// <param name="Benchmark">L'univers acheté à parts égales et conservé sur la même fenêtre de test.</param>
/// <param name="BenchmarkReturn">Rendement de cette référence sur la fenêtre, depuis le capital initial.</param>
public sealed record WalkForwardWindow(
    DateOnly TrainFrom,
    DateOnly TrainTo,
    DateOnly TestFrom,
    DateOnly TestTo,
    OptimizationReport Training,
    OptimizationCandidate? Selected,
    BacktestResult? Test,
    IReadOnlyList<EquityPoint> Benchmark,
    decimal BenchmarkReturn);

public sealed record WalkForwardProgress(int Window, int WindowCount, OptimizationProgress Optimization);

/// <param name="OutOfSampleCurve">Les fenêtres de test mises bout à bout, chacune repartant de
/// l'actif où la précédente s'est arrêtée.</param>
/// <param name="OutOfSample">Mesures de cette courbe : la seule performance qui compte.</param>
/// <param name="BenchmarkCurve">L'achat-conservation de l'univers, chaîné sur les mêmes fenêtres
/// de test : ce que la stratégie doit battre pour justifier son existence.</param>
public sealed record WalkForwardReport(
    IReadOnlyList<WalkForwardWindow> Windows,
    IReadOnlyList<EquityPoint> OutOfSampleCurve,
    PerformanceMetrics OutOfSample,
    IReadOnlyList<EquityPoint> BenchmarkCurve,
    PerformanceMetrics Benchmark)
{
    /// <summary>Part des fenêtres de test où la stratégie a fait mieux que l'achat-conservation.</summary>
    public decimal OutperformingWindowShare =>
        Windows.Count == 0 ? 0m : (decimal)Windows.Count(static w => (w.Test?.Metrics.TotalReturn ?? 0m) > w.BenchmarkReturn) / Windows.Count;

    /// <summary>Combinaisons essayées sur l'ensemble des fenêtres.</summary>
    public long Evaluated => Windows.Sum(static w => w.Training.Evaluated);

    /// <summary>Part des fenêtres de test terminées en gain.</summary>
    public decimal ProfitableWindowShare =>
        Windows.Count == 0 ? 0m : (decimal)Windows.Count(static w => w.Test?.Metrics.TotalReturn > 0m) / Windows.Count;

    /// <summary>
    /// Rendement annualisé hors échantillon rapporté à celui obtenu en apprentissage, sur les
    /// fenêtres où une combinaison a été retenue. Proche de 1, l'apprentissage se transpose ;
    /// proche de 0 ou négatif, il n'a appris que le bruit de sa fenêtre. Indéfini quand
    /// l'apprentissage lui-même ne gagnait rien.
    /// </summary>
    public decimal? Efficiency
    {
        get
        {
            var played = Windows.Where(static w => w.Selected is not null && w.Test is not null).ToArray();
            var inSample = played.Sum(static w => w.Selected!.Metrics.AnnualisedReturn);

            return inSample <= 0m ? null : played.Sum(static w => w.Test!.Metrics.AnnualisedReturn) / inSample;
        }
    }
}

/// <summary>
/// Validation en walk-forward : on optimise sur une fenêtre, on joue le gagnant sur la période
/// suivante, on décale d'une période de test et on recommence.
/// <para>Chaque combinaison retenue n'est jugée que sur des séances qu'elle n'a pas vues. Les
/// indicateurs, eux, sont calculés sur tout l'historique : causaux, ils ne lisent rien après
/// la séance qu'ils valorisent, et leur amorçage profite des séances antérieures au test.</para>
/// </summary>
public sealed class WalkForward
{
    public WalkForwardReport Run(
        WalkForwardRequest request,
        IProgress<WalkForwardProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.TrainingMonths, 1, nameof(request.TrainingMonths));
        ArgumentOutOfRangeException.ThrowIfLessThan(request.TestMonths, 1, nameof(request.TestMonths));

        var optimization = request.Optimization;
        var calendar = TradingCalendar.FromSeries(optimization.Universe).Slice(request.From, request.To);

        if (calendar.IsEmpty)
        {
            throw new ArgumentException("Aucune séance dans la plage demandée.", nameof(request));
        }

        var windows = Windows(calendar.First, calendar.Last, request.TrainingMonths, request.TestMonths);

        if (windows.Count == 0)
        {
            throw new ArgumentException(
                $"De {calendar.First} à {calendar.Last}, la plage ne contient pas une fenêtre d'apprentissage de {request.TrainingMonths} mois suivie d'un test.",
                nameof(request));
        }

        // Un seul cache pour toutes les fenêtres : les indicateurs portent sur tout l'historique,
        // une fenêtre ne change que les séances jouées.
        var cache = optimization.Cache ?? new IndicatorCache(optimization.Universe);
        var played = new List<WalkForwardWindow>(windows.Count);

        for (var index = 0; index < windows.Count; index++)
        {
            var (trainFrom, trainTo, testFrom, testTo) = windows[index];
            var window = index + 1;

            var training = new StrategyOptimizer().Run(
                optimization with { From = trainFrom, To = trainTo, Cache = cache },
                progress is null ? null : new WindowProgress(progress, window, windows.Count),
                cancellationToken);

            var selected = training.Top.Count > 0 ? training.Top[0] : null;

            var test = selected is null
                ? null
                : new BacktestEngine().Run(new BacktestRequest
                {
                    Strategy = selected.Strategy,
                    Universe = optimization.Universe,
                    InitialCash = optimization.InitialCash,
                    From = testFrom,
                    To = testTo,
                    Cache = cache,
                });

            var benchmark = BuyAndHold.Curve(optimization.Universe, testFrom, testTo, optimization.InitialCash);
            var benchmarkReturn = benchmark.Count == 0 ? 0m : (benchmark[^1].Equity / optimization.InitialCash) - 1m;

            played.Add(new WalkForwardWindow(trainFrom, trainTo, testFrom, testTo, training, selected, test, benchmark, benchmarkReturn));
        }

        var curve = Chain(played.Select(w => w.Test is { EquityCurve.Count: > 0 } test
            ? test.EquityCurve
            : InCash(calendar.Slice(w.TestFrom, w.TestTo), optimization.InitialCash)), optimization.InitialCash);

        // Les trades sont gardés tels quels : toutes les fenêtres partant du même capital, leurs
        // gains et pertes sont comparables entre eux.
        var trades = played.SelectMany(static w => w.Test?.Trades ?? []).ToArray();
        var costs = played.Sum(static w => w.Test?.Metrics.TotalCosts ?? 0m);
        var benchmarkCurve = Chain(played.Select(static w => w.Benchmark), optimization.InitialCash);

        return new WalkForwardReport(
            played,
            curve,
            PerformanceCalculator.Calculate(curve, trades, optimization.InitialCash, costs),
            benchmarkCurve,
            PerformanceCalculator.Calculate(benchmarkCurve, [], optimization.InitialCash, 0m));
    }

    /// <summary>
    /// Les fenêtres successives : apprentissage de <paramref name="trainingMonths"/> mois, test des
    /// <paramref name="testMonths"/> mois suivants, décalage d'une durée de test. La dernière
    /// fenêtre de test peut être tronquée par la fin de la plage.
    /// </summary>
    private static List<(DateOnly TrainFrom, DateOnly TrainTo, DateOnly TestFrom, DateOnly TestTo)> Windows(
        DateOnly first, DateOnly last, int trainingMonths, int testMonths)
    {
        var windows = new List<(DateOnly, DateOnly, DateOnly, DateOnly)>();

        for (var shift = 0; ; shift += testMonths)
        {
            var trainFrom = first.AddMonths(shift);
            var testFrom = trainFrom.AddMonths(trainingMonths);

            if (testFrom > last)
            {
                return windows;
            }

            var testTo = testFrom.AddMonths(testMonths).AddDays(-1);
            windows.Add((trainFrom, testFrom.AddDays(-1), testFrom, testTo < last ? testTo : last));
        }
    }

    /// <summary>
    /// Met des courbes bout à bout. Chacune a été jouée avec le capital initial ; elle est
    /// remise à l'échelle de l'actif où la précédente s'est arrêtée.
    /// </summary>
    private static List<EquityPoint> Chain(IEnumerable<IReadOnlyList<EquityPoint>> curves, decimal initialCash)
    {
        var chained = new List<EquityPoint>();
        var equity = initialCash;

        foreach (var curve in curves.Where(static c => c.Count > 0))
        {
            var scale = equity / initialCash;

            foreach (var point in curve)
            {
                chained.Add(new EquityPoint(point.Date, point.Equity * scale, point.Cash * scale, point.Invested * scale));
            }

            equity = chained[^1].Equity;
        }

        return chained;
    }

    /// <summary>Une fenêtre sans combinaison retenue : l'actif reste en liquidités, à plat.</summary>
    private static EquityPoint[] InCash(TradingCalendar sessions, decimal cash) =>
        [.. sessions.Sessions.Select(date => new EquityPoint(date, cash, cash, 0m))];

    /// <summary>Rattache la progression d'une exploration à sa fenêtre.</summary>
    private sealed class WindowProgress(IProgress<WalkForwardProgress> inner, int window, int count) : IProgress<OptimizationProgress>
    {
        public void Report(OptimizationProgress value) => inner.Report(new WalkForwardProgress(window, count, value));
    }
}
