using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Application.UseCases;

public sealed record FetchMarketDataRequest
{
    public required string Universe { get; init; }

    public required DateOnly From { get; init; }

    public DateOnly? To { get; init; }

    public string Provider { get; init; } = "yahoo";
}

public sealed record FetchMarketDataResult(int InstrumentsFetched, int BarsSaved, IReadOnlyList<string> Failures);

/// <summary>
/// Télécharge un univers et l'enregistre. La composition de l'univers vient de la
/// configuration, plus d'une liste de littéraux dans le <c>Main</c>.
/// </summary>
public sealed class FetchMarketDataHandler(
    IUniverseCatalog universes,
    IReadOnlyList<IMarketDataProvider> providers,
    IMarketDataRepository repository,
    TimeProvider clock)
{
    public async Task<FetchMarketDataResult> HandleAsync(FetchMarketDataRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = providers.FirstOrDefault(p => string.Equals(p.Name, request.Provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Source inconnue : « {request.Provider} ». Connues : {string.Join(", ", providers.Select(static p => p.Name))}.", nameof(request));

        var to = request.To ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var instruments = universes.Resolve(request.Universe);

        var saved = 0;
        var fetched = 0;
        var failures = new List<string>();

        foreach (var (symbol, name) in instruments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var bars = await provider.FetchAsync(symbol, request.From, to, cancellationToken).ConfigureAwait(false);

                if (bars.Count == 0)
                {
                    failures.Add($"{symbol} : aucune cotation retournée.");
                    continue;
                }

                saved += await repository.SaveBarsAsync(symbol, name, bars, cancellationToken).ConfigureAwait(false);
                fetched++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Un titre qui échoue ne doit pas emporter les quarante autres.
                failures.Add($"{symbol} : {exception.Message}");
            }
        }

        return new FetchMarketDataResult(fetched, saved, failures);
    }
}
