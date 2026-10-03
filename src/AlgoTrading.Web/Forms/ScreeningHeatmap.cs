using AlgoTrading.Domain.Backtesting;

namespace AlgoTrading.Web.Forms;

/// <summary>
/// Projection d'une grille de variantes sur deux réglages, pour voir un plateau d'un coup d'œil.
/// Chaque case garde la meilleure preuve des variantes qui partagent ses deux valeurs, quels que
/// soient leurs autres réglages ; une variante qui a trop peu tradé ne prouve rien et compte
/// pour zéro, comme dans le score de voisinage.
/// </summary>
/// <param name="Columns">Valeurs du réglage en abscisse, croissantes.</param>
/// <param name="Rows">Valeurs du réglage en ordonnée, croissantes ; une seule, nulle, quand la grille
/// n'a qu'un réglage qui varie.</param>
/// <param name="Cells">Meilleure preuve par case, <c>null</c> quand aucune variante ne la remplit.</param>
public sealed record ScreeningHeatmap(
    string XAxis,
    string? YAxis,
    IReadOnlyList<decimal> Columns,
    IReadOnlyList<decimal?> Rows,
    decimal?[,] Cells,
    ScreenedVariant Leader)
{
    /// <summary>
    /// La carte de la famille de la meilleure variante, sur ses deux premiers réglages qui
    /// varient. Rien quand aucun ne varie : une case seule n'apprend rien.
    /// </summary>
    public static ScreeningHeatmap? For(IReadOnlyList<ScreenedVariant> variants, string? xAxis = null, string? yAxis = null)
    {
        ArgumentNullException.ThrowIfNull(variants);

        if (variants.Count == 0)
        {
            return null;
        }

        var leader = variants[0];
        var family = variants.Where(v => v.Family == leader.Family).ToArray();
        var varying = VaryingAxes(family);

        if (varying.Count == 0)
        {
            return null;
        }

        var x = xAxis is not null && varying.Contains(xAxis) ? xAxis : varying[0];
        var y = yAxis is not null && yAxis != x && varying.Contains(yAxis)
            ? yAxis
            : varying.FirstOrDefault(axis => axis != x);

        var columns = family.Select(v => v.Axes[x]).Distinct().Order().ToArray();
        decimal?[] rows = y is null ? [null] : [.. family.Select(v => (decimal?)v.Axes[y]).Distinct().Order()];
        var cells = new decimal?[rows.Length, columns.Length];

        foreach (var variant in family)
        {
            var column = Array.IndexOf(columns, variant.Axes[x]);
            var row = y is null ? 0 : Array.IndexOf(rows, variant.Axes[y]);

            if (cells[row, column] is not { } best || variant.Evidence > best)
            {
                cells[row, column] = variant.Evidence;
            }
        }

        return new ScreeningHeatmap(x, y, columns, rows, cells, leader);
    }

    /// <summary>Réglages qui prennent plus d'une valeur dans la famille de la meilleure variante.</summary>
    public static IReadOnlyList<string> VaryingAxes(IReadOnlyList<ScreenedVariant> variants)
    {
        ArgumentNullException.ThrowIfNull(variants);

        if (variants.Count == 0)
        {
            return [];
        }

        var family = variants.Where(v => v.Family == variants[0].Family).ToArray();

        return [.. family[0].Axes.Keys.Where(axis => family.Select(v => v.Axes[axis]).Distinct().Skip(1).Any())];
    }

    /// <summary>Case de la meilleure variante, pour la souligner.</summary>
    public bool IsLeader(int row, int column) =>
        Columns[column] == Leader.Axes[XAxis] && (YAxis is null || Rows[row] == Leader.Axes[YAxis]);

    /// <summary>Plus grande preuve en valeur absolue, pour graduer l'intensité des couleurs.</summary>
    public decimal Scale()
    {
        var scale = 0m;

        foreach (var cell in Cells)
        {
            if (cell is { } value)
            {
                scale = Math.Max(scale, Math.Abs(value));
            }
        }

        return scale;
    }

    /// <summary>Libellé lisible d'un réglage : <c>parameters.period</c> devient « period ».</summary>
    public static string AxisLabel(string axis) => axis switch
    {
        "BullishBelow" => "seuil bas",
        "BearishAbove" => "seuil haut",
        "Pivot" => "pivot",
        "Weight" => "poids",
        _ when axis.StartsWith("parameters.", StringComparison.Ordinal) => axis["parameters.".Length..],
        _ => axis,
    };
}
