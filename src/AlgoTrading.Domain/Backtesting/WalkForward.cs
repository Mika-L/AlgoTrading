using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Reporting;
using AlgoTrading.Domain.Strategies;

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

    /// <summary>
    /// Cribler le catalogue <b>dans</b> chaque fenêtre d'apprentissage et n'en combiner que les
    /// meilleures variantes de chaque indicateur, ce nombre-ci. Absent, le catalogue entier est
    /// combiné. Cribler hors des fenêtres, sur une période qui recouvre les tests, ferait fuir
    /// l'avenir dans le choix des règles.
    /// </summary>
    public int? ScreenTopPerIndicator { get; init; }

    /// <summary>
    /// Ne jouer le gagnant d'une fenêtre que s'il a fait mieux que l'achat-conservation en
    /// apprentissage, selon l'objectif de classement. Sinon la fenêtre de test se passe en
    /// liquidités : ne rien trouver est une réponse, pas une raison de jouer le moins mauvais.
    /// </summary>
    public bool RequireEdge { get; init; }

    /// <summary>
    /// Les explorations antérieures sur l'univers. Chaque fenêtre d'apprentissage compte les
    /// essais de celles dont la période la recoupe ; les fenêtres d'un même walk-forward ne se
    /// comptent pas entre elles, chacune n'étant qu'un pas de la même procédure.
    /// </summary>
    public TrialHistory History { get; init; } = TrialHistory.None;
}

/// <summary>Étape d'une fenêtre de walk-forward en cours de calcul.</summary>
public enum WalkForwardStage
{
    Screening,
    Combining,
}

/// <param name="Screening">Le criblage mené dans la fenêtre d'apprentissage, s'il a été demandé.</param>
/// <param name="Catalog">Les règles combinées dans la fenêtre : le catalogue entier, ou ce que le
/// criblage en a retenu.</param>
/// <param name="Training">L'exploration menée sur la fenêtre d'apprentissage.</param>
/// <param name="Selected">La meilleure combinaison de l'apprentissage, s'il y en a une.</param>
/// <param name="Test">La combinaison retenue jouée sur la fenêtre de test, qu'elle n'a jamais vue ;
/// rien quand la fenêtre se passe en liquidités.</param>
/// <param name="CashReason">Pourquoi la fenêtre de test se passe en liquidités, le cas échéant.</param>
/// <param name="Benchmark">L'univers acheté à parts égales et conservé sur la même fenêtre de test.</param>
/// <param name="BenchmarkReturn">Rendement de cette référence sur la fenêtre, depuis le capital initial.</param>
public sealed record WalkForwardWindow(
    DateOnly TrainFrom,
    DateOnly TrainTo,
    DateOnly TestFrom,
    DateOnly TestTo,
    ScreeningReport? Screening,
    IReadOnlyList<RuleConfig> Catalog,
    OptimizationReport Training,
    OptimizationCandidate? Selected,
    BacktestResult? Test,
    string? CashReason,
    IReadOnlyList<EquityPoint> Benchmark,
    decimal BenchmarkReturn)
{
    /// <summary>Les essais d'explorations antérieures dont la période recoupe cet apprentissage.</summary>
    public TrialTally History { get; init; } = TrialTally.Empty;

    /// <summary>Les essais de la fenêtre : variantes criblées et combinaisons.</summary>
    public TrialTally Trials => (Screening?.Trials ?? TrialTally.Empty).Combine(Training.Trials);

    /// <summary>
    /// Sharpe dégonflé du gagnant sur son apprentissage. Il a été choisi au bout du criblage
    /// <b>et</b> de la combinaison, après les explorations antérieures sur ces séances : tous
    /// comptent dans le nombre d'essais.
    /// </summary>
    public decimal? DeflatedSharpe => Selected is null ? null : Trials.Deflate(Selected.Returns, History.Trials);
}

public sealed record WalkForwardProgress(int Window, int WindowCount, WalkForwardStage Stage, OptimizationProgress Optimization);

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

    /// <summary>
    /// Stratégies essayées sur l'ensemble des fenêtres, variantes criblées comprises : chacune est
    /// une chance de plus de tomber par hasard sur un bon score.
    /// </summary>
    public long Evaluated => Windows.Sum(static w => w.Training.Evaluated + (w.Screening?.Evaluated ?? 0));

    /// <summary>
    /// Probabilité que le vrai Sharpe hors échantillon soit positif. Aucune sélection ne s'est
    /// faite sur ces séances : la barre est zéro, pas le maximum d'une recherche.
    /// </summary>
    public decimal OutOfSampleSharpeProbability => SharpeStatistics.Probabilistic(ReturnMoments.Of(OutOfSampleCurve));

    /// <summary>La même probabilité pour l'achat-conservation, sur les mêmes séances.</summary>
    public decimal BenchmarkSharpeProbability => SharpeStatistics.Probabilistic(ReturnMoments.Of(BenchmarkCurve));

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

            var history = request.History.Overlapping(trainFrom, trainTo);
            var training = optimization with { From = trainFrom, To = trainTo, Cache = cache, History = history };
            ScreeningReport? screening = null;
            var catalog = optimization.Catalog;

            if (request.ScreenTopPerIndicator is { } perIndicator)
            {
                screening = new RuleScreening().Run(
                    training,
                    progress is null ? null : new WindowProgress(progress, window, windows.Count, WalkForwardStage.Screening),
                    cancellationToken);

                catalog = screening.Shortlist(perIndicator);
            }

            var explored = catalog.Count == 0
                ? OptimizationReport.Empty
                : new StrategyOptimizer().Run(
                    // Le criblage a précédé la combinaison sur les mêmes séances : ses essais comptent.
                    training with { Catalog = catalog, History = history.Combine(screening?.Trials ?? TrialTally.Empty) },
                    progress is null ? null : new WindowProgress(progress, window, windows.Count, WalkForwardStage.Combining),
                    cancellationToken);

            var selected = explored.Top.Count > 0 ? explored.Top[0] : null;

            var cashReason = (catalog.Count, selected) switch
            {
                (0, _) => "aucune variante ne passe le criblage",
                _ when explored.Evaluated == 0 => "trop peu de règles retenues pour former une combinaison",
                (_, null) => "aucune combinaison assez active",
                _ when request.RequireEdge && !(selected.Score > explored.BenchmarkScore) => "aucune ne bat le marché en apprentissage",
                _ => null,
            };

            var test = cashReason is not null
                ? null
                : new BacktestEngine().Run(new BacktestRequest
                {
                    Strategy = selected!.Strategy,
                    Universe = optimization.Universe,
                    InitialCash = optimization.InitialCash,
                    From = testFrom,
                    To = testTo,
                    Cache = cache,
                });

            var benchmark = BuyAndHold.Curve(optimization.Universe, testFrom, testTo, optimization.InitialCash);
            var benchmarkReturn = benchmark.Count == 0 ? 0m : (benchmark[^1].Equity / optimization.InitialCash) - 1m;

            played.Add(new WalkForwardWindow(
                trainFrom, trainTo, testFrom, testTo, screening, catalog, explored, selected, test, cashReason, benchmark, benchmarkReturn)
            {
                History = history,
            });
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
    private sealed class WindowProgress(IProgress<WalkForwardProgress> inner, int window, int count, WalkForwardStage stage) : IProgress<OptimizationProgress>
    {
        public void Report(OptimizationProgress value) => inner.Report(new WalkForwardProgress(window, count, stage, value));
    }
}
