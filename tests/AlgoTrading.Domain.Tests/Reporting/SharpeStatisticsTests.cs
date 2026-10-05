using AlgoTrading.Domain.Reporting;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Reporting;

/// <summary>Valeurs de référence calculées avec <c>statistics.NormalDist</c> de Python.</summary>
public class SharpeStatisticsTests
{
    private static readonly DateOnly Start = new(2024, 1, 2);

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

    [Theory]
    [InlineData(0.975, 1.9599639845400534)]
    [InlineData(0.01, -2.3263478740408408)]
    [InlineData(1e-6, -4.753424308822899)]
    public void should_invert_the_normal_distribution(double probability, double expected)
    {
        Normal.InverseCdf(probability).ShouldBe(expected, 1e-7);
    }

    [Theory]
    [InlineData(1.0, 0.8413447460685429)]
    [InlineData(-2.5, 0.006209665325776159)]
    public void should_give_the_normal_distribution(double x, double expected)
    {
        Normal.Cdf(x).ShouldBe(expected, 1e-7);
    }

    [Fact]
    public void should_give_the_probability_that_a_sharpe_is_really_positive()
    {
        var normal = new ReturnMoments(0.1, 0d, 3d, 101);

        ((double)SharpeStatistics.Probabilistic(normal)).ShouldBe(0.8407413278013518, 1e-6);
    }

    [Fact]
    public void should_doubt_a_sharpe_more_when_losses_are_rare_but_heavy()
    {
        var normal = new ReturnMoments(0.1, 0d, 3d, 101);
        var skewed = new ReturnMoments(0.1, -1d, 6d, 101);

        ((double)SharpeStatistics.Probabilistic(skewed)).ShouldBe(0.8284584164337798, 1e-6);
        SharpeStatistics.Probabilistic(skewed).ShouldBeLessThan(SharpeStatistics.Probabilistic(normal));
    }

    [Fact]
    public void should_expect_the_best_of_many_luckless_trials_as_in_the_paper()
    {
        // L'exemple numérique de Bailey et López de Prado : cent essais, variance 0,5.
        SharpeStatistics.ExpectedMaximum(100, 0.5).ShouldBe(1.789406466273208, 1e-6);
    }

    [Fact]
    public void should_raise_the_bar_with_the_number_of_trials()
    {
        var moments = new ReturnMoments(0.1, 0d, 3d, 1001);

        ((double)SharpeStatistics.Deflated(moments, 1000, 0.0004)).ShouldBe(0.8645102030039589, 1e-6);
        SharpeStatistics.Deflated(moments, 100_000, 0.0004).ShouldBeLessThan(SharpeStatistics.Deflated(moments, 1000, 0.0004));
    }

    [Fact]
    public void should_not_deflate_a_strategy_that_was_never_selected()
    {
        var moments = new ReturnMoments(0.05, 0d, 3d, 500);

        SharpeStatistics.Deflated(moments, 1, 0.0004).ShouldBe(SharpeStatistics.Probabilistic(moments));
    }

    [Fact]
    public void should_measure_the_moments_of_daily_returns()
    {
        var moments = ReturnMoments.Of(Curve([0.01m, 0.01m, 0.01m, 0.01m, 0.05m]));

        moments.Observations.ShouldBe(5);
        moments.Sharpe.ShouldBeGreaterThan(0d);
        // Un seul gros gain parmi de petits : la queue droite domine.
        moments.Skewness.ShouldBeGreaterThan(0d);
    }

    [Fact]
    public void should_find_nothing_to_test_in_a_flat_curve()
    {
        var moments = ReturnMoments.Of(Curve([0m, 0m, 0m, 0m]));

        moments.ShouldBe(ReturnMoments.None);
        SharpeStatistics.Probabilistic(moments).ShouldBe(0m);
    }

    [Fact]
    public void should_track_the_dispersion_of_trial_sharpes()
    {
        var dispersion = new SharpeDispersion();

        foreach (var sharpe in new[] { 0.01, 0.03, -0.02, 0.04 })
        {
            dispersion.Add(sharpe);
        }

        // Variance d'échantillon de { 0,01 ; 0,03 ; −0,02 ; 0,04 }.
        dispersion.Variance.ShouldBe(0.0007, 1e-12);
    }
}
