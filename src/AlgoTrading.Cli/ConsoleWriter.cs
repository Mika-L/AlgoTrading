using System.Globalization;
using AlgoTrading.Application.Ports;

namespace AlgoTrading.Cli;

/// <summary>
/// Sortie destinée à l'utilisateur. Elle ne passe <b>pas</b> par la journalisation :
/// sans cette séparation, un <c>--verbosity quiet</c> masquerait les résultats du backtest
/// en même temps que les traces de diagnostic.
/// </summary>
public sealed class ConsoleWriter : IConsoleWriter
{
    public void Write(string text) => Console.Write(text);

    public void WriteLine(string text = "") => Console.WriteLine(text);

    public void WriteWarning(string text)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Error.WriteLine(text);
        Console.ForegroundColor = previous;
    }

    public void WriteTable(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        var widths = new int[headers.Count];

        for (var i = 0; i < headers.Count; i++)
        {
            widths[i] = headers[i].Length;
        }

        foreach (var row in rows)
        {
            for (var i = 0; i < Math.Min(row.Count, widths.Length); i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        Console.WriteLine(string.Join("  ", headers.Select((h, i) => h.PadRight(widths[i]))).TrimEnd());
        Console.WriteLine(string.Join("  ", widths.Select(static w => new string('─', w))));

        foreach (var row in rows)
        {
            Console.WriteLine(string.Join("  ", row.Select((c, i) => i < widths.Length ? c.PadRight(widths[i]) : c)).TrimEnd());
        }
    }

    /// <summary>Met en forme une fraction en pourcentage — la seule multiplication par cent du projet.</summary>
    public static string Percent(decimal fraction) => (fraction * 100m).ToString("0.00", CultureInfo.CurrentCulture) + " %";

    public static string Money(decimal amount) => amount.ToString("N2", CultureInfo.CurrentCulture) + " €";
}
