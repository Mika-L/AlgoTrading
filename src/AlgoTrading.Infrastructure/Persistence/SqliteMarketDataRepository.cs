using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.MarketData;
using AlgoTrading.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlgoTrading.Infrastructure.Persistence;

/// <summary>
/// Charge et enregistre les cotations.
/// <para>C'est ici que se fait le <b>rétro-ajustement</b> : le facteur
/// <c>clôture ajustée / clôture</c> est appliqué à l'ouverture, au plus haut, au plus bas et
/// à la clôture, le volume étant divisé par ce même facteur. Le domaine ne voit donc jamais
/// qu'une seule échelle de prix, et le mélange qui touchait sept indicateurs devient
/// inexprimable.</para>
/// </summary>
public sealed class SqliteMarketDataRepository(
    IDbContextFactory<AlgoTradingDbContext> contextFactory,
    IOptions<UniverseOptions> universeOptions,
    ILogger<SqliteMarketDataRepository> logger) : IMarketDataRepository
{
    /// <summary>
    /// Rapport de prix au-delà duquel une variation quotidienne ne peut plus être un
    /// mouvement de marché : c'est une opération sur titre non répercutée.
    /// </summary>
    private const decimal DiscontinuityRatio = 4m;

    private HashSet<string> Excluded => [.. universeOptions.Value.Exclusions.Select(static e => e.Trim().ToUpperInvariant())];

    public async Task<IReadOnlyList<Instrument>> ListInstrumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var excluded = Excluded;

        var rows = await context.Instruments
            .AsNoTracking()
            .OrderBy(i => i.Symbol)
            .Select(i => new
            {
                i.Symbol,
                i.Name,
                Count = i.Bars.Count,
                First = i.Bars.Min(b => (DateOnly?)b.Date),
                Last = i.Bars.Max(b => (DateOnly?)b.Date),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows
                .Where(r => !excluded.Contains(r.Symbol.ToUpperInvariant()))
                .Select(r => new Instrument(Symbol.From(r.Symbol), r.Name)
                {
                    BarCount = r.Count,
                    FirstBar = r.First,
                    LastBar = r.Last,
                }),
        ];
    }

    public async Task<BarSeries> LoadSeriesAsync(Symbol symbol, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await LoadAsync(context, symbol, from, to, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BarSeries>> LoadUniverseAsync(
        IReadOnlyList<Symbol> symbols,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbols);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var series = new List<BarSeries>(symbols.Count);

        foreach (var symbol in symbols.Distinct().Order())
        {
            series.Add(await LoadAsync(context, symbol, from, to, cancellationToken).ConfigureAwait(false));
        }

        return series;
    }

    public async Task<int> SaveBarsAsync(Symbol symbol, string name, IReadOnlyList<RawBar> bars, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bars);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var ticker = symbol.Ticker;
        var instrument = await context.Instruments.FirstOrDefaultAsync(i => i.Symbol == ticker, cancellationToken).ConfigureAwait(false);

        if (instrument is null)
        {
            instrument = new InstrumentRow { Symbol = ticker, Name = name };
            context.Instruments.Add(instrument);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var known = await context.Bars
            .Where(b => b.InstrumentId == instrument.Id)
            .Select(b => b.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existing = known.ToHashSet();
        var added = 0;

        foreach (var bar in bars.OrderBy(static b => b.Date))
        {
            if (!existing.Add(bar.Date) || !IsSane(symbol, bar))
            {
                continue;
            }

            context.Bars.Add(new DailyBarRow
            {
                InstrumentId = instrument.Id,
                Date = bar.Date,
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                AdjustedClose = bar.AdjustedClose,
                Volume = bar.Volume,
            });

            added++;
        }

        // Un seul aller-retour en fin de lot, là où le code d'origine appelait SaveChanges()
        // à chaque ligne, en pleine boucle.
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return added;
    }

    public async Task<IReadOnlyList<HistoryGap>> FindGapsAsync(Symbol? symbol = null, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = context.Instruments.AsNoTracking();

        if (symbol is { } only)
        {
            var ticker = only.Ticker;
            query = query.Where(i => i.Symbol == ticker);
        }

        var gaps = new List<HistoryGap>();

        foreach (var instrument in await query.OrderBy(i => i.Symbol).ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            var dates = await context.Bars
                .AsNoTracking()
                .Where(b => b.InstrumentId == instrument.Id)
                .OrderBy(b => b.Date)
                .Select(b => b.Date)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            for (var i = 1; i < dates.Count; i++)
            {
                var missing = BusinessDaysBetween(dates[i - 1], dates[i]);

                // Une semaine ouvrée sans cotation n'est pas un jour férié : c'est un trou.
                if (missing >= 5)
                {
                    gaps.Add(new HistoryGap(Symbol.From(instrument.Symbol), dates[i - 1], dates[i], missing));
                }
            }
        }

        return gaps;
    }

    public async Task<IReadOnlyList<PriceDiscontinuity>> FindDiscontinuitiesAsync(Symbol? symbol = null, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = context.Instruments.AsNoTracking();

        if (symbol is { } only)
        {
            var ticker = only.Ticker;
            query = query.Where(i => i.Symbol == ticker);
        }

        var found = new List<PriceDiscontinuity>();

        foreach (var instrument in await query.OrderBy(i => i.Symbol).ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            var closes = await context.Bars
                .AsNoTracking()
                .Where(b => b.InstrumentId == instrument.Id)
                .OrderBy(b => b.Date)
                .Select(b => new { b.Date, b.AdjustedClose })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            for (var i = 1; i < closes.Count; i++)
            {
                var previous = closes[i - 1].AdjustedClose;
                var current = closes[i].AdjustedClose;

                if (previous <= 0m || current <= 0m)
                {
                    continue;
                }

                var ratio = current / previous;

                if (ratio >= DiscontinuityRatio || ratio <= 1m / DiscontinuityRatio)
                {
                    found.Add(new PriceDiscontinuity(Symbol.From(instrument.Symbol), closes[i].Date, previous, current));
                }
            }
        }

        return found;
    }

    private async Task<BarSeries> LoadAsync(AlgoTradingDbContext context, Symbol symbol, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var ticker = symbol.Ticker;

        var query = context.Bars
            .AsNoTracking()
            .Where(b => b.Instrument!.Symbol == ticker);

        if (from is { } start)
        {
            query = query.Where(b => b.Date >= start);
        }

        if (to is { } end)
        {
            query = query.Where(b => b.Date <= end);
        }

        var rows = await query.OrderBy(b => b.Date).ToListAsync(cancellationToken).ConfigureAwait(false);

        var bars = new List<PriceBar>(rows.Count);
        var rejected = 0;

        foreach (var row in rows)
        {
            if (Adjust(row) is { } bar)
            {
                bars.Add(bar);
            }
            else
            {
                rejected++;
            }
        }

        if (rejected > 0)
        {
            logger.LogWarning("{Symbol} : {Rejected} séance(s) écartée(s) pour incohérence des prix.", symbol, rejected);
        }

        WarnOnDiscontinuities(symbol, bars);

        return BarSeries.Create(symbol, bars);
    }

    /// <summary>
    /// Applique le facteur d'ajustement à toute la barre, et rejette celles dont les prix
    /// sont incohérents plutôt que de les laisser fausser un indicateur.
    /// </summary>
    private static PriceBar? Adjust(DailyBarRow row)
    {
        if (row.Close <= 0m || row.Open <= 0m || row.High <= 0m || row.Low <= 0m || row.AdjustedClose <= 0m)
        {
            return null;
        }

        var factor = row.AdjustedClose / row.Close;

        // Le facteur s'applique aux quatre prix de la même façon, clôture comprise. Prendre
        // directement la clôture ajustée casserait l'ordre O/H/L/C par arrondi décimal :
        // une barre dont le plus haut égale la clôture serait rejetée à tort.
        var open = row.Open * factor;
        var high = row.High * factor;
        var low = row.Low * factor;
        var close = row.Close * factor;
        var volume = factor == 0m ? row.Volume : (long)decimal.Round(row.Volume / factor);

        if (high < low || high < open || high < close || low > open || low > close)
        {
            return null;
        }

        return new PriceBar(row.Date, open, high, low, close, Math.Max(0L, volume), row.Close);
    }

    /// <summary>
    /// Signale une rupture de prix au chargement plutôt que de la laisser fabriquer une
    /// plus-value fictive. Le titre n'est pas écarté d'autorité : c'est à la configuration
    /// de décider, pas à une condition enfouie dans le code.
    /// </summary>
    private void WarnOnDiscontinuities(Symbol symbol, List<PriceBar> bars)
    {
        for (var i = 1; i < bars.Count; i++)
        {
            var previous = bars[i - 1].Close;
            var current = bars[i].Close;

            if (previous <= 0m)
            {
                continue;
            }

            var ratio = current / previous;

            if (ratio >= DiscontinuityRatio || ratio <= 1m / DiscontinuityRatio)
            {
                logger.LogWarning(
                    "{Symbol} {Date:yyyy-MM-dd} : le cours passe de {Previous} à {Current} en une séance. Opération sur titre non répercutée — écartez ce titre par « Universes:Exclusions » ou corrigez ses données.",
                    symbol,
                    bars[i].Date,
                    previous,
                    current);
            }
        }
    }

    private static int BusinessDaysBetween(DateOnly from, DateOnly to)
    {
        var missing = 0;

        for (var day = from.AddDays(1); day < to; day = day.AddDays(1))
        {
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                missing++;
            }
        }

        return missing;
    }

    private bool IsSane(Symbol symbol, RawBar bar)
    {
        if (bar.Open <= 0m || bar.High <= 0m || bar.Low <= 0m || bar.Close <= 0m || bar.AdjustedClose <= 0m || bar.Volume < 0L)
        {
            logger.LogWarning("{Symbol} {Date:yyyy-MM-dd} : séance ignorée, prix ou volume non valides.", symbol, bar.Date);
            return false;
        }

        if (bar.High < bar.Low || bar.High < bar.Open || bar.High < bar.Close || bar.Low > bar.Open || bar.Low > bar.Close)
        {
            logger.LogWarning("{Symbol} {Date:yyyy-MM-dd} : séance ignorée, plus haut et plus bas incohérents.", symbol, bar.Date);
            return false;
        }

        return true;
    }
}
