namespace AlgoTrading.Domain.Reporting;

/// <summary>
/// Ce qu'une courbe d'actif dit de ses rendements quotidiens, pour juger son Sharpe. Le Sharpe
/// est <b>par séance</b>, sans annualisation : c'est l'unité des formules qui l'éprouvent.
/// </summary>
/// <param name="Sharpe">Rendement quotidien moyen rapporté à son écart type.</param>
/// <param name="Skewness">Asymétrie des rendements : négative, les pertes sont plus rares mais plus fortes.</param>
/// <param name="Kurtosis">Aplatissement, non diminué de 3 : une loi normale vaut 3, des queues épaisses davantage.</param>
/// <param name="Observations">Nombre de rendements quotidiens mesurés.</param>
public sealed record ReturnMoments(double Sharpe, double Skewness, double Kurtosis, int Observations)
{
    /// <summary>Une courbe à plat, ou trop courte : rien à éprouver, aucune observation retenue.</summary>
    public static ReturnMoments None { get; } = new(0d, 0d, 3d, 0);

    public static ReturnMoments Of(IReadOnlyList<EquityPoint> curve)
    {
        ArgumentNullException.ThrowIfNull(curve);

        if (curve.Count < 3)
        {
            return None;
        }

        var returns = new double[curve.Count - 1];
        for (var i = 1; i < curve.Count; i++)
        {
            var previous = curve[i - 1].Equity;
            returns[i - 1] = previous == 0m ? 0d : (double)((curve[i].Equity / previous) - 1m);
        }

        var mean = returns.Average();
        double second = 0d, third = 0d, fourth = 0d;

        foreach (var value in returns)
        {
            var deviation = value - mean;
            var squared = deviation * deviation;
            second += squared;
            third += squared * deviation;
            fourth += squared * squared;
        }

        second /= returns.Length;
        third /= returns.Length;
        fourth /= returns.Length;

        // Même convention que le Sharpe des mesures : écart type de population.
        var deviationOfReturns = Math.Sqrt(second);

        // Un écart type à l'échelle du bruit d'arrondi décimal vaut une courbe à plat : sans
        // risque pris, il n'y a pas de Sharpe à éprouver, et pas une chance sur deux qu'il soit positif.
        return deviationOfReturns < 1e-12
            ? None
            : new ReturnMoments(
                mean / deviationOfReturns,
                third / (second * deviationOfReturns),
                fourth / (second * second),
                returns.Length);
    }
}

/// <summary>
/// Le Sharpe éprouvé contre le hasard (Bailey et López de Prado, « The Deflated Sharpe Ratio »,
/// 2014).
/// <para>Le Sharpe <b>probabiliste</b> est la probabilité que le vrai Sharpe dépasse une barre,
/// compte tenu de la durée mesurée et de la forme des rendements : peu de séances, une
/// asymétrie négative ou des queues épaisses le font douter.</para>
/// <para>Le Sharpe <b>dégonflé</b> place cette barre au meilleur Sharpe qu'auraient donné
/// autant d'essais sans aucun talent. Le gagnant de trois mille combinaisons a forcément un
/// beau Sharpe ; la question est s'il fait mieux que ce que trois mille tirages au hasard
/// auraient sorti.</para>
/// </summary>
public static class SharpeStatistics
{
    private const double EulerMascheroni = 0.5772156649015329;

    /// <summary>Le seuil usuel : en deçà, rien ne distingue le résultat de la chance.</summary>
    public const decimal Significant = 0.95m;

    /// <summary>
    /// Probabilité que le vrai Sharpe par séance dépasse <paramref name="benchmark"/>. Zéro sans
    /// au moins deux rendements mesurés.
    /// </summary>
    public static decimal Probabilistic(ReturnMoments moments, double benchmark = 0d)
    {
        ArgumentNullException.ThrowIfNull(moments);

        if (moments.Observations < 2)
        {
            return 0m;
        }

        var sharpe = moments.Sharpe;

        // L'écart type de l'estimateur du Sharpe, corrigé de l'asymétrie et de l'aplatissement.
        // Des moments extrêmes peuvent rendre l'expression négative : elle est alors bornée.
        var spread = 1d - (moments.Skewness * sharpe) + ((moments.Kurtosis - 1d) / 4d * sharpe * sharpe);
        var z = (sharpe - benchmark) * Math.Sqrt(moments.Observations - 1) / Math.Sqrt(Math.Max(spread, 1e-12));

        return (decimal)Normal.Cdf(z);
    }

    /// <summary>
    /// Le meilleur Sharpe par séance attendu de <paramref name="trials"/> essais sans talent,
    /// dont les Sharpe se dispersent avec cette <paramref name="variance"/>. Nul pour un essai
    /// unique : il n'y a pas eu de sélection.
    /// </summary>
    public static double ExpectedMaximum(long trials, double variance)
    {
        if (trials < 2 || variance <= 0d)
        {
            return 0d;
        }

        var n = (double)trials;

        return Math.Sqrt(variance) * (
            ((1d - EulerMascheroni) * Normal.InverseCdf(1d - (1d / n)))
            + (EulerMascheroni * Normal.InverseCdf(1d - (1d / (n * Math.E)))));
    }

    /// <summary>
    /// Probabilité que le vrai Sharpe dépasse le meilleur que le hasard aurait donné en autant
    /// d'essais. Au-delà de 95 %, la sélection a vraisemblablement trouvé quelque chose.
    /// </summary>
    public static decimal Deflated(ReturnMoments moments, long trials, double variance) =>
        Probabilistic(moments, ExpectedMaximum(trials, variance));
}

/// <summary>
/// La dispersion des Sharpe de tous les essais d'une exploration, tenue au fil de l'eau
/// (algorithme de Welford) : rien n'est gardé des essais eux-mêmes.
/// </summary>
public sealed class SharpeDispersion
{
    private readonly Lock _gate = new();
    private long _count;
    private double _mean;
    private double _squares;

    public void Add(double sharpe)
    {
        lock (_gate)
        {
            _count++;
            var delta = sharpe - _mean;
            _mean += delta / _count;
            _squares += delta * (sharpe - _mean);
        }
    }

    /// <summary>Variance d'échantillon des Sharpe par séance ; nulle en deçà de deux essais.</summary>
    public double Variance
    {
        get
        {
            lock (_gate)
            {
                return _count < 2 ? 0d : _squares / (_count - 1);
            }
        }
    }
}

/// <summary>La loi normale centrée réduite.</summary>
internal static class Normal
{
    /// <summary>Fonction de répartition, par la fonction d'erreur complémentaire.</summary>
    public static double Cdf(double x) => 0.5 * Erfc(-x / Math.Sqrt(2d));

    /// <summary>
    /// Réciproque de la fonction de répartition (algorithme d'Acklam, erreur relative de l'ordre
    /// de 1e-9, ample pour une probabilité affichée en pour cent).
    /// </summary>
    public static double InverseCdf(double p)
    {
        if (p <= 0d)
        {
            return double.NegativeInfinity;
        }

        if (p >= 1d)
        {
            return double.PositiveInfinity;
        }

        const double low = 0.02425;

        if (p < low)
        {
            var q = Math.Sqrt(-2d * Math.Log(p));
            return Tail(q);
        }

        if (p > 1d - low)
        {
            var q = Math.Sqrt(-2d * Math.Log(1d - p));
            return -Tail(q);
        }

        var r = p - 0.5;
        var s = r * r;

        return (((((-3.969683028665376e+01 * s + 2.209460984245205e+02) * s - 2.759285104469687e+02) * s + 1.383577518672690e+02) * s - 3.066479806614716e+01) * s + 2.506628277459239e+00) * r
            / (((((-5.447609879822406e+01 * s + 1.615858368580409e+02) * s - 1.556989798598866e+02) * s + 6.680131188771972e+01) * s - 1.328068155288572e+01) * s + 1d);

        static double Tail(double q) =>
            (((((-7.784894002430293e-03 * q - 3.223964580411365e-01) * q - 2.400758277161838e+00) * q - 2.549732539343734e+00) * q + 4.374664141464968e+00) * q + 2.938163982698783e+00)
            / ((((7.784695709041462e-03 * q + 3.224671290700398e-01) * q + 2.445134137142996e+00) * q + 3.754408661907416e+00) * q + 1d);
    }

    /// <summary>Fonction d'erreur complémentaire (Numerical Recipes, erreur relative inférieure à 1,2e-7).</summary>
    private static double Erfc(double x)
    {
        var z = Math.Abs(x);
        var t = 1d / (1d + (0.5 * z));
        double[] coefficients = [-1.26551223, 1.00002368, 0.37409196, 0.09678418, -0.18628806, 0.27886807, -1.13520398, 1.48851587, -0.82215223, 0.17087277];

        // Le polynôme en t, par Horner, du terme de plus haut degré au terme constant.
        var polynomial = 0d;
        for (var i = coefficients.Length - 1; i >= 0; i--)
        {
            polynomial = (polynomial * t) + coefficients[i];
        }

        var r = t * Math.Exp(-(z * z) + polynomial);

        return x >= 0d ? r : 2d - r;
    }
}
