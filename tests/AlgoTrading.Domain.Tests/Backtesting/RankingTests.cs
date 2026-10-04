using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.Reporting;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Backtesting;

public class RankingTests
{
    private static readonly DateOnly Start = new(2024, 1, 2);

    /// <summary>Une courbe partant de 100 000 et suivant ces rendements quotidiens.</summary>
    private static List<EquityPoint> Curve(IEnumerable<decimal> returns)
    {
        var equity = 100_000m;
        var curve = new List<EquityPoint> { new(Start, equity, equity, 0m) };

        foreach (var r in returns)
        {
            equity *= 1m + r;
            curve.Add(new(Start.AddDays(curve.Count), equity, equity, 0m));
        }

        return curve;
    }

    private static readonly decimal[] Market = [0.01m, -0.01m, 0.02m, -0.005m, 0.003m, -0.012m];

    [Fact]
    public void should_give_the_market_no_edge_over_itself()
    {
        var market = Curve(Market);

        PerformanceCalculator.InformationRatio(market, market).ShouldBe(0m);
    }

    [Fact]
    public void should_annualise_the_daily_excess_over_its_volatility()
    {
        // Un écart au marché de 0,2 % une séance sur deux, nul sinon : moyenne 0,1 %, écart-type 0,1 %.
        var excess = Market.Select(static (_, i) => i % 2 == 0 ? 0.002m : 0m).ToArray();
        var strategy = Curve(Market.Zip(excess, static (m, e) => m + e));

        PerformanceCalculator.InformationRatio(strategy, Curve(Market)).ShouldBe((decimal)Math.Sqrt(252), 0.0001m);
    }

    [Fact]
    public void should_score_negatively_a_curve_that_lags_the_market()
    {
        var lagging = Curve(Market.Select(static (m, i) => m - (i % 2 == 0 ? 0.003m : 0.001m)));

        PerformanceCalculator.InformationRatio(lagging, Curve(Market)).ShouldBeLessThan(0m);
    }

    [Fact]
    public void should_only_compare_sessions_both_curves_share()
    {
        var market = Curve(Market);
        var strategy = Curve(Market.Select(static (m, i) => m + (i % 2 == 0 ? 0.002m : 0m)));

        // Les deux dernières séances manquent à la référence : seules les quatre premières comptent.
        var truncated = PerformanceCalculator.InformationRatio(strategy, market[..^2]);

        truncated.ShouldBe(PerformanceCalculator.InformationRatio(strategy[..^2], market[..^2]));
    }

    [Theory]
    [InlineData(RankingObjective.Calmar, 1.5)]
    [InlineData(RankingObjective.Sharpe, 0.7)]
    public void should_read_the_score_from_the_metrics_for_calmar_and_sharpe(RankingObjective objective, double expected)
    {
        var metrics = PerformanceMetrics.Empty with { Calmar = 1.5m, Sharpe = 0.7m };

        Ranking.Score(objective, metrics, [], []).ShouldBe((decimal)expected);
    }

    [Fact]
    public void should_score_the_information_ratio_against_the_benchmark()
    {
        var strategy = Curve(Market.Select(static (m, i) => m + (i % 2 == 0 ? 0.002m : 0m)));
        var market = Curve(Market);

        Ranking.Score(RankingObjective.InformationRatio, PerformanceMetrics.Empty, strategy, market)
            .ShouldBe(PerformanceCalculator.InformationRatio(strategy, market));
    }
}
