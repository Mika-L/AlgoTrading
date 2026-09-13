using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Reporting;

namespace AlgoTrading.Application.UseCases;

public sealed record GenerateReportRequest
{
    public required BacktestResult Result { get; init; }

    public required string OutputDirectory { get; init; }

    /// <summary>Formats souhaités ; vide, tous les exports disponibles sont produits.</summary>
    public IReadOnlyList<string> Formats { get; init; } = [];
}

public sealed record GenerateReportResponse(IReadOnlyList<string> Files);

public sealed class GenerateReportHandler(IReadOnlyList<IReportSink> sinks)
{
    public async Task<GenerateReportResponse> HandleAsync(GenerateReportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wanted = request.Formats.Count == 0
            ? sinks
            : [.. sinks.Where(s => request.Formats.Contains(s.Name, StringComparer.OrdinalIgnoreCase))];

        if (wanted.Count == 0)
        {
            throw new ArgumentException($"Format inconnu. Disponibles : {string.Join(", ", sinks.Select(static s => s.Name))}.", nameof(request));
        }

        var files = new List<string>(wanted.Count);

        foreach (var sink in wanted)
        {
            files.Add(await sink.WriteAsync(request.Result, request.OutputDirectory, cancellationToken).ConfigureAwait(false));
        }

        return new GenerateReportResponse(files);
    }
}
