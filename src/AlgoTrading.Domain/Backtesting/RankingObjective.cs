using AlgoTrading.Domain.Reporting;

namespace AlgoTrading.Domain.Backtesting;

/// <summary>Ce qu'une exploration cherche à maximiser.</summary>
public enum RankingObjective
{
    /// <summary>Rendement annualisé rapporté à la pire baisse.</summary>
    Calmar,

    /// <summary>Rendement quotidien moyen rapporté à sa volatilité, annualisé.</summary>
    Sharpe,

    /// <summary>
    /// Écart quotidien au marché rapporté à la volatilité de cet écart, annualisé. Seul des
    /// trois à juger une stratégie <b>contre</b> l'achat-conservation de son univers : retrancher
    /// le rendement du marché, identique pour toutes les combinaisons d'une même fenêtre, ne
    /// changerait aucun classement ; le rapporter au risque pris pour s'en écarter, si.
    /// </summary>
    InformationRatio,
}

public static class Ranking
{
    /// <summary>Le nom de l'objectif, tel qu'il s'affiche.</summary>
    public static string Name(RankingObjective objective) => objective switch
    {
        RankingObjective.Calmar => "Calmar",
        RankingObjective.Sharpe => "Sharpe",
        RankingObjective.InformationRatio => "Ratio d'information",
        _ => throw new ArgumentOutOfRangeException(nameof(objective), objective, "Objectif de classement inconnu."),
    };

    /// <summary>Le score d'une courbe selon l'objectif, la référence servant au ratio d'information.</summary>
    public static decimal Score(
        RankingObjective objective,
        PerformanceMetrics metrics,
        IReadOnlyList<EquityPoint> curve,
        IReadOnlyList<EquityPoint> benchmark)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return objective switch
        {
            RankingObjective.Calmar => metrics.Calmar,
            RankingObjective.Sharpe => metrics.Sharpe,
            RankingObjective.InformationRatio => PerformanceCalculator.InformationRatio(curve, benchmark),
            _ => throw new ArgumentOutOfRangeException(nameof(objective), objective, "Objectif de classement inconnu."),
        };
    }
}
