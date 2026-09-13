using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.MarketData;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace AlgoTrading.Infrastructure.Persistence;

public sealed record LegacyImportReport(int Instruments, int Bars, int Skipped);

/// <summary>
/// Reprend l'historique de l'ancienne base.
/// <para>Elle ne peut pas être mise à niveau sur place : son schéma venait d'un
/// <c>EnsureCreated()</c> et non de migrations, elle n'a donc pas de table d'historique de
/// migrations. On repart d'une base neuve et on y verse les cotations.</para>
/// <para>C'est le plan de repli si le re-téléchargement échoue — l'endpoint de la source
/// n'est pas contractuel et peut exiger un jeton sans préavis.</para>
/// </summary>
public sealed class LegacyDatabaseImporter(IMarketDataRepository repository, ILogger<LegacyDatabaseImporter> logger)
{
    public async Task<LegacyImportReport> ImportAsync(string legacyDatabasePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyDatabasePath);

        await using var connection = new SqliteConnection($"Data Source={legacyDatabasePath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var instruments = await ReadInstrumentsAsync(connection, cancellationToken).ConfigureAwait(false);

        var importedInstruments = 0;
        var importedBars = 0;
        var skipped = 0;

        foreach (var (id, ticker, name) in instruments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (bars, ignored) = await ReadBarsAsync(connection, id, cancellationToken).ConfigureAwait(false);
            skipped += ignored;

            if (bars.Count == 0)
            {
                logger.LogWarning("{Symbol} : aucune séance exploitable dans l'ancienne base.", ticker);
                continue;
            }

            importedBars += await repository.SaveBarsAsync(Symbol.From(ticker), name, bars, cancellationToken).ConfigureAwait(false);
            importedInstruments++;
        }

        return new LegacyImportReport(importedInstruments, importedBars, skipped);
    }

    private static async Task<List<(int Id, string Symbol, string Name)>> ReadInstrumentsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Symbol, Name FROM Stocks ORDER BY Symbol";

        var instruments = new List<(int, string, string)>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            instruments.Add((reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2)));
        }

        return instruments;
    }

    private static async Task<(List<RawBar> Bars, int Skipped)> ReadBarsAsync(SqliteConnection connection, int stockId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Date, Open, High, Low, Close, AdjustedClose, Volume
            FROM StockPriceHistories
            WHERE StockId = $stockId
            ORDER BY Date
            """;
        command.Parameters.AddWithValue("$stockId", stockId);

        var bars = new List<RawBar>();
        var skipped = 0;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!TryReadDate(reader.GetValue(0), out var date))
            {
                skipped++;
                continue;
            }

            var open = reader.GetDecimal(1);
            var high = reader.GetDecimal(2);
            var low = reader.GetDecimal(3);
            var close = reader.GetDecimal(4);
            var adjusted = reader.IsDBNull(5) ? close : reader.GetDecimal(5);
            var volume = reader.IsDBNull(6) ? 0L : reader.GetInt64(6);

            if (open <= 0m || high <= 0m || low <= 0m || close <= 0m || adjusted <= 0m)
            {
                skipped++;
                continue;
            }

            bars.Add(new RawBar(date, open, high, low, close, adjusted, Math.Max(0L, volume)));
        }

        return (bars, skipped);
    }

    /// <summary>L'ancienne base stocke les dates en texte ; leur format n'est pas garanti.</summary>
    private static bool TryReadDate(object value, out DateOnly date)
    {
        switch (value)
        {
            case DateTime moment:
                date = DateOnly.FromDateTime(moment);
                return true;

            case string text when DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var parsed):
                date = DateOnly.FromDateTime(parsed);
                return true;

            default:
                date = default;
                return false;
        }
    }
}
