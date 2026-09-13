using AlgoTrading.Domain.Reporting;

namespace AlgoTrading.Application.Ports;

/// <summary>
/// Sortie destinée à l'utilisateur — à ne pas confondre avec la journalisation de diagnostic.
/// <para>La distinction est nécessaire : sans elle, un <c>--verbosity quiet</c> masquerait
/// les résultats du backtest en même temps que les traces.</para>
/// </summary>
public interface IConsoleWriter
{
    void Write(string text);

    void WriteLine(string text = "");

    void WriteTable(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows);

    void WriteWarning(string text);
}

/// <summary>Export d'un résultat vers un fichier — CSV, image.</summary>
public interface IReportSink
{
    string Name { get; }

    Task<string> WriteAsync(BacktestResult result, string outputDirectory, CancellationToken cancellationToken = default);
}
