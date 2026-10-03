using System.Diagnostics;

namespace AlgoTrading.Web.Components.Shared;

/// <summary>
/// Relaie la progression d'un calcul vers l'écran sans le noyer : les rapports arrivent des fils
/// de l'exploration parallèle, parfois des centaines par seconde, et l'affichage n'est
/// rafraîchi qu'au plus tous les <c>interval</c>. Le dernier état est toujours conservé.
/// </summary>
public sealed class ThrottledProgress<T>(Action<T> store, Func<Task> refresh, TimeSpan interval) : IProgress<T>
{
    private readonly Lock _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan? _last;

    public void Report(T value)
    {
        lock (_gate)
        {
            store(value);

            var now = _clock.Elapsed;

            if (_last is { } last && now - last < interval)
            {
                return;
            }

            _last = now;
        }

        _ = refresh();
    }
}
