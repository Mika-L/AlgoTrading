using AlgoTrading.Application.Ports;
using AlgoTrading.Domain.Reporting;
using ScottPlot;

namespace AlgoTrading.Infrastructure.Reporting;

/// <summary>Trace la courbe d'actif et le profil de baisse depuis les sommets.</summary>
public sealed class ScottPlotChartRenderer : IReportSink
{
    public string Name => "chart";

    public Task<string> WriteAsync(BacktestResult result, string outputDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        if (result.EquityCurve.Count == 0)
        {
            throw new InvalidOperationException("Il n'y a rien à tracer : la courbe d'actif est vide.");
        }

        var dates = result.EquityCurve.Select(static p => p.Date.ToDateTime(TimeOnly.MinValue)).ToArray();
        var equity = result.EquityCurve.Select(static p => (double)p.Equity).ToArray();

        var plot = new Plot();

        var curve = plot.Add.Scatter(dates, equity);
        curve.LegendText = "Valeur du portefeuille";
        curve.MarkerSize = 0;

        var drawdown = DrawdownSeries(result);
        var underwater = plot.Add.Scatter(dates, drawdown);
        underwater.LegendText = "Baisse depuis le sommet (%)";
        underwater.MarkerSize = 0;
        underwater.Axes.YAxis = plot.Axes.Right;

        plot.Axes.DateTimeTicksBottom();
        plot.Axes.Left.Label.Text = "Valeur (€)";
        plot.Axes.Right.Label.Text = "Baisse (%)";
        plot.Title($"{result.StrategyName} - {result.From:yyyy-MM-dd} a {result.To:yyyy-MM-dd}");
        plot.ShowLegend();

        var path = Path.Combine(outputDirectory, $"{Slug(result.StrategyName)}-{result.Fingerprint[..8]}.png");
        plot.SavePng(path, 1_200, 700);

        return Task.FromResult(path);
    }

    private static double[] DrawdownSeries(BacktestResult result)
    {
        var series = new double[result.EquityCurve.Count];
        var peak = result.EquityCurve[0].Equity;

        for (var i = 0; i < series.Length; i++)
        {
            var equity = result.EquityCurve[i].Equity;

            if (equity > peak)
            {
                peak = equity;
            }

            series[i] = peak == 0m ? 0d : -(double)((peak - equity) / peak) * 100d;
        }

        return series;
    }

    private static string Slug(string name) =>
        string.Concat(name.Select(static c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')).Trim('-');
}
