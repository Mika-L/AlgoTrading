using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Reporting;

namespace AlgoTrading.Application.Ports;

public enum ExplorationKind
{
    Screening,
    Optimization,
    WalkForwardWindow,
}

/// <summary>
/// Une exploration menée jusqu'au bout : sur quels titres et quelles séances elle a choisi, et
/// combien d'essais elle a faits pour cela.
/// </summary>
/// <param name="From">Première séance sur laquelle les essais ont été jugés.</param>
/// <param name="To">Dernière séance.</param>
/// <param name="Trials">Le nombre d'essais et la dispersion de leurs Sharpe.</param>
public sealed record ExplorationEntry(
    ExplorationKind Kind,
    IReadOnlyList<Symbol> Universe,
    DateOnly From,
    DateOnly To,
    TrialTally Trials)
{
    /// <summary>Attribué à l'enregistrement.</summary>
    public int Id { get; init; }

    /// <summary>Attribué à l'enregistrement.</summary>
    public DateTimeOffset RanAt { get; init; }

    /// <summary>Le catalogue exploré, tel que l'utilisateur l'a désigné.</summary>
    public string? Catalog { get; init; }

    public RankingObjective Objective { get; init; } = RankingObjective.Calmar;

    /// <summary>Le gagnant de l'exploration, s'il y en a eu un.</summary>
    public string? Best { get; init; }

    /// <summary>Son Sharpe dégonflé, explorations antérieures comprises.</summary>
    public decimal? BestDeflatedSharpe { get; init; }
}

/// <summary>
/// Le journal des explorations. Il ne garde pas les résultats, seulement de quoi compter les
/// essais d'une session à l'autre : sans lui, relancer un criblage avec un autre catalogue
/// remettrait le compteur du Sharpe dégonflé à zéro, et chaque relance paraîtrait plus
/// convaincante qu'elle ne l'est.
/// </summary>
public interface IExplorationJournal
{
    /// <summary>Les explorations déjà menées sur exactement ces titres.</summary>
    Task<TrialHistory> HistoryAsync(IReadOnlyList<Symbol> universe, CancellationToken cancellationToken = default);

    Task RecordAsync(IReadOnlyList<ExplorationEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>Les dernières explorations, de la plus récente à la plus ancienne.</summary>
    Task<IReadOnlyList<ExplorationEntry>> ListAsync(int limit = 20, CancellationToken cancellationToken = default);
}
