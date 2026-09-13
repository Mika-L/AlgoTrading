using AlgoTrading.Domain.Indicators;
using Shouldly;

namespace AlgoTrading.Domain.Tests.Indicators;

public class SeriesTests
{
    [Fact]
    public void should_average_the_last_three_closes()
    {
        // SMA(3) sur [1,2,3,4,5] : (1+2+3)/3=2, (2+3+4)/3=3, (3+4+5)/3=4
        decimal[] source = [1m, 2m, 3m, 4m, 5m];

        var sma = Series.Sma(source, 3, out var firstValid);

        firstValid.ShouldBe(2);
        sma[2].ShouldBe(2m);
        sma[3].ShouldBe(3m);
        sma[4].ShouldBe(4m);
    }

    [Fact]
    public void should_report_no_valid_average_when_history_is_shorter_than_the_period()
    {
        decimal[] source = [1m, 2m];

        Series.Sma(source, 5, out var firstValid);

        firstValid.ShouldBe(source.Length);
    }

    [Fact]
    public void should_seed_the_exponential_average_on_the_simple_average_of_the_first_period()
    {
        // EMA(3) sur [1,2,3,4,5] : amorce (1+2+3)/3=2 ; k=2/4=0,5
        // puis 2 + 0,5·(4-2) = 3 ; 3 + 0,5·(5-3) = 4
        decimal[] source = [1m, 2m, 3m, 4m, 5m];

        var ema = Series.Ema(source, 3, out var firstValid);

        firstValid.ShouldBe(2);
        ema[2].ShouldBe(2m);
        ema[3].ShouldBe(3m);
        ema[4].ShouldBe(4m);
    }

    [Fact]
    public void should_smooth_the_wilder_way_by_adding_one_period_fraction_of_the_gap()
    {
        // Wilder(3) sur [3,3,3,9] : amorce 3 ; puis 3 + (9-3)/3 = 5
        decimal[] source = [3m, 3m, 3m, 9m];

        var smoothed = Series.WilderSmooth(source, 3, out var firstValid);

        firstValid.ShouldBe(2);
        smoothed[2].ShouldBe(3m);
        smoothed[3].ShouldBe(5m);
    }

    [Fact]
    public void should_chain_calculations_by_propagating_the_warmup_of_the_source()
    {
        // Une source qui ne commence qu'au 3ᵉ point : la SMA(2) qui la consomme
        // doit démarrer au 4ᵉ, pas au 2ᵉ.
        decimal[] source = [0m, 0m, 4m, 6m, 8m];

        var sma = Series.Sma(source, 2, out var firstValid, sourceFirstValid: 2);

        firstValid.ShouldBe(3);
        sma[3].ShouldBe(5m);
        sma[4].ShouldBe(7m);
    }

    [Fact]
    public void should_track_the_highest_value_of_the_rolling_window()
    {
        decimal[] source = [3m, 1m, 4m, 1m, 5m, 9m, 2m, 6m];

        var highest = Series.HighestHigh(source, 3, out var firstValid);

        firstValid.ShouldBe(2);
        highest[2].ShouldBe(4m);
        highest[3].ShouldBe(4m);
        highest[4].ShouldBe(5m);
        highest[5].ShouldBe(9m);
        highest[6].ShouldBe(9m);
        highest[7].ShouldBe(9m);
    }

    [Fact]
    public void should_track_the_lowest_value_of_the_rolling_window()
    {
        decimal[] source = [3m, 1m, 4m, 1m, 5m, 9m, 2m, 6m];

        var lowest = Series.LowestLow(source, 3, out var firstValid);

        firstValid.ShouldBe(2);
        lowest[2].ShouldBe(1m);
        lowest[3].ShouldBe(1m);
        lowest[4].ShouldBe(1m);
        lowest[5].ShouldBe(1m);
        lowest[6].ShouldBe(2m);
        lowest[7].ShouldBe(2m);
    }

    [Fact]
    public void should_let_the_window_extremum_expire_once_it_leaves_the_window()
    {
        decimal[] source = [10m, 1m, 2m, 3m];

        var highest = Series.HighestHigh(source, 2, out _);

        highest[1].ShouldBe(10m);
        highest[2].ShouldBe(2m);
        highest[3].ShouldBe(3m);
    }

    [Fact]
    public void should_measure_the_dispersion_around_the_window_average()
    {
        // StdDev population de [2,4,4,4,5,5,7,9] sur 8 points : moyenne 5, variance 4, écart type 2
        decimal[] source = [2m, 4m, 4m, 4m, 5m, 5m, 7m, 9m];

        var stdDev = Series.StdDev(source, 8, out var firstValid);

        firstValid.ShouldBe(7);
        stdDev[7].ShouldBe(2m, 0.0000001m);
    }

    [Fact]
    public void should_average_the_absolute_gaps_to_the_window_average()
    {
        // [1,2,3,4] : moyenne 2,5 ; écarts 1,5+0,5+0,5+1,5 = 4 ; 4/4 = 1
        decimal[] source = [1m, 2m, 3m, 4m];

        var deviation = Series.MeanAbsoluteDeviation(source, 4, out var firstValid);

        firstValid.ShouldBe(3);
        deviation[3].ShouldBe(1m);
    }

    [Fact]
    public void should_widen_the_true_range_to_cover_the_gap_with_the_previous_close()
    {
        decimal[] high = [12m, 20m];
        decimal[] low = [10m, 18m];
        decimal[] close = [11m, 19m];

        var trueRange = Series.TrueRange(high, low, close, out var firstValid);

        // La première séance n'a pas de true range : il lui manque une clôture précédente.
        firstValid.ShouldBe(1);
        trueRange[1].ShouldBe(9m);   // |20 - 11| domine l'amplitude du jour (2)
    }

    [Fact]
    public void should_shift_values_by_whole_bars_never_by_calendar_days()
    {
        decimal[] source = [1m, 2m, 3m, 4m];

        var shifted = Series.Shift(source, 2, out var firstValid);

        firstValid.ShouldBe(2);
        shifted[2].ShouldBe(1m);
        shifted[3].ShouldBe(2m);
    }

    [Fact]
    public void should_refuse_to_read_a_later_bar_because_that_would_be_look_ahead()
    {
        decimal[] source = [1m, 2m, 3m];

        Should.Throw<ArgumentOutOfRangeException>(() => Series.Shift(source, -1, out _));
    }

    [Fact]
    public void should_measure_the_change_over_the_requested_number_of_bars()
    {
        decimal[] source = [10m, 12m, 9m, 15m];

        var delta = Series.Delta(source, 2, out var firstValid);

        firstValid.ShouldBe(2);
        delta[2].ShouldBe(-1m);
        delta[3].ShouldBe(3m);
    }

    [Fact]
    public void should_subtract_two_series_from_the_later_of_their_two_warmups()
    {
        decimal[] left = [0m, 5m, 8m];
        decimal[] right = [0m, 0m, 3m];

        var difference = Series.Subtract(left, right, out var firstValid, leftFirstValid: 1, rightFirstValid: 2);

        firstValid.ShouldBe(2);
        difference[2].ShouldBe(5m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void should_reject_a_period_that_is_not_strictly_positive(int period)
    {
        decimal[] source = [1m, 2m, 3m];

        Should.Throw<ArgumentOutOfRangeException>(() => Series.Sma(source, period, out _));
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(9, 3)]
    [InlineData(6.25, 2.5)]
    public void should_compute_an_exact_square_root_when_there_is_one(decimal value, decimal expected)
    {
        Series.Sqrt(value).ShouldBe(expected);
    }

    [Fact]
    public void should_compute_an_irrational_square_root_beyond_double_precision()
    {
        // Un double plafonne à ~15 chiffres significatifs ; l'écart type d'un prix en decimal
        // doit en garder 28. La valeur attendue est celle de racine de 2 à 28 décimales.
        Series.Sqrt(2m).ShouldBe(1.4142135623730950488016887242m);
    }
}
