using AlgoTrading.Domain.Backtesting;

namespace AlgoTrading.Domain.Reporting;

/// <summary>
/// Mesures d'un backtest. Tous les taux sont des <b>fractions</b> — <c>0,02</c> vaut 2 % —
/// conformément à la convention du domaine ; la multiplication par cent n'existe que dans
/// le formatage.
/// </summary>
public sealed record PerformanceMetrics
{
    public required decimal TotalReturn { get; init; }

    /// <summary>Taux de croissance annuel composé.</summary>
    public required decimal AnnualisedReturn { get; init; }

    /// <summary>Plus forte baisse depuis un sommet, en fraction.</summary>
    public required decimal MaxDrawdown { get; init; }

    /// <summary>Rendement annualisé rapporté à la pire baisse. Critère de classement par défaut.</summary>
    public required decimal Calmar { get; init; }

    public required decimal Sharpe { get; init; }

    public required decimal Volatility { get; init; }

    public required int TradeCount { get; init; }

    public required decimal WinRate { get; init; }

    public required decimal ProfitFactor { get; init; }

    public required decimal AverageWin { get; init; }

    public required decimal AverageLoss { get; init; }

    public required decimal TotalCosts { get; init; }

    public static PerformanceMetrics Empty { get; } = new()
    {
        TotalReturn = 0m,
        AnnualisedReturn = 0m,
        MaxDrawdown = 0m,
        Calmar = 0m,
        Sharpe = 0m,
        Volatility = 0m,
        TradeCount = 0,
        WinRate = 0m,
        ProfitFactor = 0m,
        AverageWin = 0m,
        AverageLoss = 0m,
        TotalCosts = 0m,
    };
}

/// <summary>Calcule les mesures à partir d'une courbe d'actif et d'un relevé de trades.</summary>
public static class PerformanceCalculator
{
    /// <summary>Nombre de séances par an sur les places européennes, pour l'annualisation.</summary>
    private const int SessionsPerYear = 252;

    public static PerformanceMetrics Calculate(
        IReadOnlyList<EquityPoint> curve,
        IReadOnlyList<Trade> trades,
        decimal initialCash,
        decimal totalCosts)
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(trades);

        if (curve.Count == 0 || initialCash <= 0m)
        {
            return PerformanceMetrics.Empty with { TradeCount = trades.Count, TotalCosts = totalCosts };
        }

        var finalEquity = curve[^1].Equity;
        var totalReturn = (finalEquity / initialCash) - 1m;

        var years = Math.Max(curve.Count / (double)SessionsPerYear, 1d / SessionsPerYear);
        var annualised = finalEquity <= 0m
            ? -1m
            : DecimalMath.Pow(finalEquity / initialCash, 1d / years) - 1m;

        var maxDrawdown = MaxDrawdown(curve);
        var (volatility, sharpe) = RiskAdjusted(curve);

        var winners = trades.Where(static t => t.IsWinner).ToArray();
        var losers = trades.Where(static t => !t.IsWinner).ToArray();
        var grossProfit = winners.Sum(static t => t.NetPnL);
        var grossLoss = Math.Abs(losers.Sum(static t => t.NetPnL));

        return new PerformanceMetrics
        {
            TotalReturn = totalReturn,
            AnnualisedReturn = annualised,
            MaxDrawdown = maxDrawdown,

            // Maximiser le rendement brut sur une période unique est du surapprentissage :
            // le classement par défaut rapporte le gain au pire creux traversé.
            Calmar = maxDrawdown == 0m ? 0m : annualised / maxDrawdown,
            Sharpe = sharpe,
            Volatility = volatility,
            TradeCount = trades.Count,
            WinRate = trades.Count == 0 ? 0m : (decimal)winners.Length / trades.Count,
            ProfitFactor = grossLoss == 0m ? 0m : grossProfit / grossLoss,
            AverageWin = winners.Length == 0 ? 0m : grossProfit / winners.Length,
            AverageLoss = losers.Length == 0 ? 0m : -grossLoss / losers.Length,
            TotalCosts = totalCosts,
        };
    }

    private static decimal MaxDrawdown(IReadOnlyList<EquityPoint> curve)
    {
        var peak = curve[0].Equity;
        var worst = 0m;

        foreach (var point in curve)
        {
            if (point.Equity > peak)
            {
                peak = point.Equity;
            }

            if (peak > 0m)
            {
                var drawdown = (peak - point.Equity) / peak;
                if (drawdown > worst)
                {
                    worst = drawdown;
                }
            }
        }

        return worst;
    }

    private static (decimal Volatility, decimal Sharpe) RiskAdjusted(IReadOnlyList<EquityPoint> curve)
    {
        if (curve.Count < 3)
        {
            return (0m, 0m);
        }

        var returns = new decimal[curve.Count - 1];
        for (var i = 1; i < curve.Count; i++)
        {
            var previous = curve[i - 1].Equity;
            returns[i - 1] = previous == 0m ? 0m : (curve[i].Equity / previous) - 1m;
        }

        var mean = returns.Average();

        var variance = 0m;
        foreach (var value in returns)
        {
            var deviation = value - mean;
            variance += deviation * deviation;
        }

        variance /= returns.Length;

        var dailyVolatility = DecimalMath.Sqrt(variance);
        var annualFactor = DecimalMath.Sqrt(SessionsPerYear);

        var volatility = dailyVolatility * annualFactor;
        var sharpe = dailyVolatility == 0m ? 0m : mean / dailyVolatility * annualFactor;

        return (volatility, sharpe);
    }
}
