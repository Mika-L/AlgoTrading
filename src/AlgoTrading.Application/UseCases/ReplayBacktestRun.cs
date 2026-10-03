using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Application.UseCases;

public sealed record ReplayBacktestRunResponse(BacktestRunSummary Summary, RunBacktestResponse Replay);

/// <summary>
/// Rejoue un run enregistré. Seuls ses chiffres de synthèse sont persistés : la courbe et le
/// relevé de trades se reconstituent depuis la stratégie, la période, le capital et l'univers
/// du run — c'est ce que garantit l'empreinte.
/// </summary>
public sealed class ReplayBacktestRunHandler(IBacktestRunStore store, RunBacktestHandler runner)
{
    /// <returns><c>null</c> si le run est inconnu ou n'a pas conservé sa stratégie.</returns>
    public async Task<ReplayBacktestRunResponse?> HandleAsync(int runId, CancellationToken cancellationToken = default)
    {
        var summary = await store.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        var strategy = await store.GetStrategyAsync(runId, cancellationToken).ConfigureAwait(false);

        if (summary is null || strategy is null)
        {
            return null;
        }

        var replay = await runner.HandleAsync(new RunBacktestRequest
        {
            Strategy = strategy,
            Universe = [.. summary.Universe.Select(Symbol.From)],
            From = summary.From,
            To = summary.To,
            InitialCash = summary.InitialCash,
        }, cancellationToken).ConfigureAwait(false);

        return new ReplayBacktestRunResponse(summary, replay);
    }
}
