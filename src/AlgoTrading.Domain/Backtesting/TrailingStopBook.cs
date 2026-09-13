using AlgoTrading.Domain.Indicators;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Domain.Backtesting;

/// <summary>
/// Les stops suiveurs en cours, un niveau par titre en portefeuille.
/// <para>Le niveau ne recule jamais. Il monte avec le plus haut de chaque séance et reste sur
/// place quand le cours redescend : un stop suiveur suit le plus haut atteint, pas le cours du
/// jour. Il tient bon, de même, quand la volatilité s'élargit — <c>plus haut − k × ATR</c>
/// peut baisser d'une séance à l'autre, un stop suiveur non.</para>
/// <para>Des deux formes, la fraction et le multiple d'ATR, c'est la plus protectrice qui
/// vaut lorsque les deux sont configurées.</para>
/// <para>Le niveau opposable à une séance est arrêté à la clôture de la <b>veille</b>, ATR
/// compris. C'est ce qui le tient hors du look-ahead : au moment où le moteur compare le plus
/// bas du jour à ce niveau, ce niveau ne doit rien au jour qu'il juge.</para>
/// </summary>
internal sealed class TrailingStopBook(RiskPolicy risk, IReadOnlyDictionary<Symbol, IndicatorResult> atr)
{
    private readonly Dictionary<Symbol, decimal> _levels = [];

    /// <summary>
    /// Niveau opposable à la séance <paramref name="bar"/>, <c>null</c> tant qu'aucune forme
    /// n'a de quoi le poser. La première séance d'une position le pose sous le prix de
    /// revient, à la distance qu'indiquait la veille.
    /// </summary>
    public decimal? LevelFor(Symbol symbol, decimal entryPrice, int bar)
    {
        if (_levels.TryGetValue(symbol, out var level))
        {
            return level;
        }

        if (Candidate(symbol, entryPrice, bar - 1) is not { } opened)
        {
            return null;
        }

        _levels[symbol] = opened;

        return opened;
    }

    /// <summary>Monte le cliquet avec la séance qui vient de s'écouler, jamais l'inverse.</summary>
    public void Advance(Symbol symbol, decimal high, int bar)
    {
        if (Candidate(symbol, high, bar) is not { } candidate)
        {
            return;
        }

        if (!_levels.TryGetValue(symbol, out var level) || candidate > level)
        {
            _levels[symbol] = candidate;
        }
    }

    public void Forget(Symbol symbol) => _levels.Remove(symbol);

    /// <summary>
    /// Oublie les titres sortis du portefeuille — soldés sur signal, le moteur ne passant
    /// pas par ici pour cela. Un rachat ultérieur repart du prix de revient de sa ligne et
    /// non du cliquet de la précédente.
    /// </summary>
    public void Retain(IReadOnlyDictionary<Symbol, Position> positions)
    {
        if (_levels.Count == 0)
        {
            return;
        }

        foreach (var symbol in _levels.Keys.ToArray())
        {
            if (!positions.ContainsKey(symbol))
            {
                _levels.Remove(symbol);
            }
        }
    }

    /// <summary>
    /// Ce que les stops configurés placent sous <paramref name="reference"/>, le plus haut
    /// des deux l'emportant. L'ATR est lu à la barre demandée, qui n'est pas celle jugée.
    /// </summary>
    private decimal? Candidate(Symbol symbol, decimal reference, int bar)
    {
        decimal? level = risk.TrailingRate is { } rate ? reference * (1m - rate) : null;

        if (risk.TrailingAtr is { } trailing && atr[symbol].TryGetValue(IndicatorLines.Value, bar, out var value))
        {
            var fromAtr = reference - (trailing.Multiple * value);

            if (fromAtr > (level ?? decimal.MinValue))
            {
                level = fromAtr;
            }
        }

        return level;
    }
}
