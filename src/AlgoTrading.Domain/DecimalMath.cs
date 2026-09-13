namespace AlgoTrading.Domain;

/// <summary>
/// Les quelques fonctions que <see cref="decimal"/> n'offre pas. Neutre vis-à-vis des
/// modules : indicateurs et mesures de performance s'en servent sans dépendre l'un de l'autre.
/// </summary>
public static class DecimalMath
{
    /// <summary>Racine carrée par Newton-Raphson, amorcée en double puis raffinée en decimal.</summary>
    public static decimal Sqrt(decimal value)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "La racine carrée d'un nombre négatif n'est pas définie.");
        }

        if (value == 0m)
        {
            return 0m;
        }

        var guess = (decimal)Math.Sqrt((double)value);
        if (guess <= 0m)
        {
            guess = value;
        }

        for (var i = 0; i < 16; i++)
        {
            var next = (guess + (value / guess)) / 2m;
            if (next == guess)
            {
                break;
            }

            guess = next;
        }

        return guess;
    }

    /// <summary>
    /// Élévation à une puissance réelle, en double. Réservée aux <b>mesures</b> — taux annualisé,
    /// jamais à un calcul dont dépend une décision de trading.
    /// </summary>
    public static decimal Pow(decimal value, double exponent) => (decimal)Math.Pow((double)value, exponent);
}
