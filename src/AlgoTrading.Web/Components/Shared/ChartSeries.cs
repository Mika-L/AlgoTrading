namespace AlgoTrading.Web.Components.Shared;

public readonly record struct ChartPoint(DateOnly Date, double Value);

/// <summary>
/// Une série tracée. <see cref="Slot"/> est sa place dans la palette catégorielle, et non son
/// rang à l'écran : une série garde sa couleur quand on en retire une autre.
/// </summary>
public sealed record ChartSeries(string Name, int Slot, IReadOnlyList<ChartPoint> Points, bool Area = false)
{
    public string Color => $"var(--series-{(Slot % 8) + 1})";
}
