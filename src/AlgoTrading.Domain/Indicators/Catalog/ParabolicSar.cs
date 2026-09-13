using AlgoTrading.Domain.MarketData;

namespace AlgoTrading.Domain.Indicators.Catalog;

/// <summary>
/// Parabolic SAR de Wilder. Ex-<c>ParabolicSAR</c>.
/// <para>Deux règles du livre absentes du code d'origine sont rétablies : le SAR ne peut
/// jamais pénétrer l'amplitude des deux barres précédentes, et une accélération ne s'applique
/// qu'à un nouvel extrême — sans quoi le point de retournement dérive.</para>
/// </summary>
public sealed class ParabolicSar : IndicatorBase
{
    public const string Kind = "Sar";

    public ParabolicSar(decimal step = 0.02m, decimal maxStep = 0.2m)
    {
        Step = RequirePositive(step, nameof(step));
        MaxStep = RequirePositive(maxStep, nameof(maxStep));

        if (step > maxStep)
        {
            throw new ArgumentException($"Le pas d'accélération ({step}) ne peut pas dépasser son plafond ({maxStep}).", nameof(step));
        }

        Descriptor = IndicatorDescriptor.Of(Kind, ("step", step), ("maxStep", maxStep));
    }

    public decimal Step { get; }

    public decimal MaxStep { get; }

    public override IndicatorDescriptor Descriptor { get; }

    public override int WarmupBars => 1;

    protected override IndicatorResult ComputeCore(BarSeries bars)
    {
        var count = bars.Count;
        if (count < 2)
        {
            return Empty(count, IndicatorLines.Value);
        }

        var high = bars.High;
        var low = bars.Low;
        var close = bars.Close;

        var sar = new decimal[count];

        // La tendance initiale est déduite de la première variation de clôture.
        var isUptrend = close[1] >= close[0];
        var extremePoint = isUptrend ? high[0] : low[0];
        var current = isUptrend ? low[0] : high[0];
        var acceleration = Step;

        for (var i = 1; i < count; i++)
        {
            current += acceleration * (extremePoint - current);

            if (isUptrend)
            {
                // En hausse, le SAR reste sous les plus bas des deux séances précédentes.
                current = Math.Min(current, low[i - 1]);
                if (i >= 2)
                {
                    current = Math.Min(current, low[i - 2]);
                }

                if (low[i] < current)
                {
                    isUptrend = false;
                    current = extremePoint;
                    extremePoint = low[i];
                    acceleration = Step;
                }
                else if (high[i] > extremePoint)
                {
                    extremePoint = high[i];
                    acceleration = Math.Min(acceleration + Step, MaxStep);
                }
            }
            else
            {
                current = Math.Max(current, high[i - 1]);
                if (i >= 2)
                {
                    current = Math.Max(current, high[i - 2]);
                }

                if (high[i] > current)
                {
                    isUptrend = true;
                    current = extremePoint;
                    extremePoint = high[i];
                    acceleration = Step;
                }
                else if (low[i] < extremePoint)
                {
                    extremePoint = low[i];
                    acceleration = Math.Min(acceleration + Step, MaxStep);
                }
            }

            sar[i] = current;
        }

        return IndicatorResult.Single(Descriptor, sar, 1);
    }
}
