using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Application.Ports;

/// <summary>Un instrument connu du référentiel.</summary>
public sealed record Instrument(Symbol Symbol, string Name)
{
    public int BarCount { get; init; }

    public DateOnly? FirstBar { get; init; }

    public DateOnly? LastBar { get; init; }
}

/// <summary>Trou constaté dans un historique : des séances attendues, absentes.</summary>
public sealed record HistoryGap(Symbol Symbol, DateOnly From, DateOnly To, int MissingSessions);

/// <summary>
/// Rupture de prix d'une séance à l'autre, trop forte pour être un mouvement de marché.
/// <para>Elle signale presque toujours une opération sur titre — regroupement, division —
/// que la source n'a pas répercutée sur l'historique. Laissée passer, elle fabrique une
/// plus-value fictive qui fausse tout le backtest.</para>
/// </summary>
public sealed record PriceDiscontinuity(Symbol Symbol, DateOnly Date, decimal PreviousClose, decimal Close)
{
    public decimal Ratio => PreviousClose == 0m ? 0m : Close / PreviousClose;
}

/// <summary>
/// Accès à l'historique de marché. Le domaine ne le connaît pas : c'est l'application qui
/// charge les séries et les lui passe.
/// </summary>
public interface IMarketDataRepository
{
    Task<IReadOnlyList<Instrument>> ListInstrumentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Charge une série <b>entièrement rétro-ajustée</b>, prête pour les indicateurs.</summary>
    Task<BarSeries> LoadSeriesAsync(Symbol symbol, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BarSeries>> LoadUniverseAsync(IReadOnlyList<Symbol> symbols, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);

    /// <summary>Enregistre des barres brutes ; les doublons de date sont ignorés.</summary>
    Task<int> SaveBarsAsync(Symbol symbol, string name, IReadOnlyList<RawBar> bars, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoryGap>> FindGapsAsync(Symbol? symbol = null, CancellationToken cancellationToken = default);

    /// <summary>Ruptures de prix évoquant une opération sur titre non répercutée.</summary>
    Task<IReadOnlyList<PriceDiscontinuity>> FindDiscontinuitiesAsync(Symbol? symbol = null, CancellationToken cancellationToken = default);
}
