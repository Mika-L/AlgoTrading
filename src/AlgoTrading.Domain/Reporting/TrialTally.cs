namespace AlgoTrading.Domain.Reporting;

/// <summary>
/// Ce que des essais laissent derrière eux pour éprouver un gagnant : leur nombre et la
/// dispersion de leurs Sharpe par séance. Deux décomptes se fusionnent sans revoir les essais
/// (Chan, Golub et LeVeque, 1979) : le criblage et les combinaisons d'une même fenêtre, ou les
/// explorations d'une session à l'autre.
/// </summary>
/// <param name="Trials">Nombre d'essais.</param>
/// <param name="MeanSharpe">Sharpe par séance moyen des essais.</param>
/// <param name="SquaredDeviations">Somme des carrés des écarts à cette moyenne.</param>
public sealed record TrialTally(long Trials, double MeanSharpe, double SquaredDeviations)
{
    public static TrialTally Empty { get; } = new(0, 0d, 0d);

    /// <summary>Variance d'échantillon des Sharpe par séance ; nulle en deçà de deux essais.</summary>
    public double Variance => Trials < 2 ? 0d : SquaredDeviations / (Trials - 1);

    public TrialTally Combine(TrialTally other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other.Trials == 0)
        {
            return this;
        }

        if (Trials == 0)
        {
            return other;
        }

        var trials = Trials + other.Trials;
        var delta = other.MeanSharpe - MeanSharpe;

        return new TrialTally(
            trials,
            MeanSharpe + (delta * other.Trials / trials),
            SquaredDeviations + other.SquaredDeviations + (delta * delta * Trials * other.Trials / trials));
    }

    /// <summary>
    /// Probabilité que le vrai Sharpe dépasse le meilleur qu'auraient donné, au hasard, les essais
    /// de ce décompte et <paramref name="priorTrials"/> essais antérieurs.
    /// <para>Les essais antérieurs relèvent le nombre, pas la dispersion : celle-ci reste celle
    /// des essais de ce décompte. Mise en commun, elle se rapprocherait de celle d'une exploration
    /// relancée à l'identique, et relancer pourrait alors rendre un gagnant plus convaincant.</para>
    /// </summary>
    public decimal Deflate(ReturnMoments moments, long priorTrials = 0) =>
        SharpeStatistics.Deflated(moments, Trials + priorTrials, Variance);
}

/// <summary>Les essais d'une exploration passée, et les séances sur lesquelles elle a choisi.</summary>
public sealed record PastTrials(DateOnly From, DateOnly To, TrialTally Tally);

/// <summary>
/// Les explorations déjà menées sur un univers. Relancer un criblage sur les mêmes séances
/// avec un autre catalogue, c'est reposer la question aux mêmes données : chaque essai passé
/// est une chance de plus d'être tombé par hasard sur un beau score.
/// </summary>
public sealed class TrialHistory(IReadOnlyList<PastTrials> explorations)
{
    public static TrialHistory None { get; } = new([]);

    public IReadOnlyList<PastTrials> Explorations { get; } = explorations ?? throw new ArgumentNullException(nameof(explorations));

    /// <summary>
    /// Les essais des explorations dont la période recoupe celle-ci, ne serait-ce que d'une
    /// séance : elles ont vu une partie des données sur lesquelles on choisit maintenant.
    /// </summary>
    public TrialTally Overlapping(DateOnly from, DateOnly to) =>
        Explorations
            .Where(past => past.From <= to && past.To >= from)
            .Aggregate(TrialTally.Empty, static (total, past) => total.Combine(past.Tally));
}
