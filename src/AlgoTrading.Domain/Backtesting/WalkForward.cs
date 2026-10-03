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
public sealed record WalkForwardWindow(
    DateOnly TrainFrom,
    DateOnly TrainTo,
    DateOnly TestFrom,
    DateOnly TestTo,
    OptimizationReport Training,
    OptimizationCandidate? Selected,
    BacktestResult? Test);

public sealed record WalkForwardProgress(int Window, int WindowCount, OptimizationProgress Optimization);

/// <param name="OutOfSampleCurve">Les fenêtres de test mises bout à bout, chacune repartant de
/// l'actif où la précédente s'est arrêtée.</param>
/// <param name="OutOfSample">Mesures de cette courbe : la seule performance qui compte.</param>
public sealed record WalkForwardReport(
    IReadOnlyList<WalkForwardWindow> Windows,
    IReadOnlyList<EquityPoint> OutOfSampleCurve,
    PerformanceMetrics OutOfSample)
{
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

            played.Add(new WalkForwardWindow(trainFrom, trainTo, testFrom, testTo, training, selected, test));
        }

        var (curve, trades, costs) = Stitch(played, calendar, optimization.InitialCash);

        return new WalkForwardReport(
            played,
            curve,
            PerformanceCalculator.Calculate(curve, trades, optimization.InitialCash, costs));
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
    /// Met les fenêtres de test bout à bout. Chacune a été jouée avec le capital de départ ; sa
    /// courbe est remise à l'échelle de l'actif où la précédente s'est arrêtée. Une fenêtre sans
    /// combinaison retenue reste en liquidités, à plat.
    /// <para>Les trades, eux, sont gardés tels quels : toutes les fenêtres partant du même
    /// capital, leurs gains et pertes sont comparables entre eux.</para>
    /// </summary>
    private static (List<EquityPoint> Curve, List<Trade> Trades, decimal Costs) Stitch(
        IReadOnlyList<WalkForwardWindow> windows, TradingCalendar calendar, decimal initialCash)
    {
        var curve = new List<EquityPoint>();
        var trades = new List<Trade>();
        var costs = 0m;
        var equity = initialCash;

        foreach (var window in windows)
        {
            if (window.Test is not { EquityCurve.Count: > 0 } test)
            {
                foreach (var date in calendar.Slice(window.TestFrom, window.TestTo).Sessions)
                {
                    curve.Add(new EquityPoint(date, equity, equity, 0m));
                }

                continue;
            }

            var scale = equity / test.InitialCash;

            foreach (var point in test.EquityCurve)
            {
                curve.Add(new EquityPoint(point.Date, point.Equity * scale, point.Cash * scale, point.Invested * scale));
            }

            equity = curve[^1].Equity;
            trades.AddRange(test.Trades);
            costs += test.Metrics.TotalCosts * scale;
        }

        return (curve, trades, costs);
    }

    /// <summary>Rattache la progression d'une exploration à sa fenêtre.</summary>
    private sealed class WindowProgress(IProgress<WalkForwardProgress> inner, int window, int count) : IProgress<OptimizationProgress>
    {
        public void Report(OptimizationProgress value) => inner.Report(new WalkForwardProgress(window, count, value));
    }
}
