using System.Globalization;
using AlgoTrading.Domain.Backtesting;
using AlgoTrading.Domain.Strategies;
using AlgoTrading.Domain.Strategies.Rules;

namespace AlgoTrading.Web.Forms;

/// <summary>Libellés et formats d'affichage. La multiplication par cent n'existe qu'ici.</summary>
public static class Labels
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static IReadOnlyList<(string Type, string Label, string Hint)> RuleTypes { get; } =
    [
        (ThresholdRule.Type, "Seuils", "Haussier sous le seuil bas, baissier au-dessus du seuil haut."),
        (CrossoverRule.Type, "Croisement", "Deux lignes de l'indicateur : au croisement, ou tant que l'une domine l'autre."),
        (BandBreakoutRule.Type, "Sortie de bande", "Clôture hors des bandes : retour à la moyenne ou suivi de tendance."),
        (PriceVsLevelRule.Type, "Prix contre niveau", "Clôture au-dessus ou au-dessous d'une ligne de l'indicateur."),
        (SignRule.Type, "Signe", "Ligne de part et d'autre d'un pivot."),
        (IchimokuCloudRule.Type, "Nuage Ichimoku", "Clôture et nuage alignés."),
    ];

    public static string RuleType(string type) => RuleTypes.FirstOrDefault(t => t.Type == type).Label ?? type;

    public static string RuleHint(string type) => RuleTypes.FirstOrDefault(t => t.Type == type).Hint ?? string.Empty;

    public static string Aggregation(AggregationMode mode) => mode switch
    {
        AggregationMode.Consensus => "Unanimité",
        AggregationMode.Majority => "Majorité",
        AggregationMode.Any => "Au moins une voix",
        AggregationMode.Weighted => "Pondérée",
        _ => mode.ToString(),
    };

    public static string Sizing(SizingMode mode) => mode switch
    {
        SizingMode.EquityFraction => "Part du portefeuille",
        SizingMode.FixedNotional => "Montant fixe",
        _ => mode.ToString(),
    };

    public static string Crossover(CrossoverMode mode) => mode switch
    {
        CrossoverMode.Cross => "Au croisement (événement)",
        CrossoverMode.Relative => "Tant qu'elle domine (état)",
        _ => mode.ToString(),
    };

    public static string Exit(ExitMode mode) => mode switch
    {
        ExitMode.CloseAll => "Solder la position",
        ExitMode.Fraction => "En sortir une fraction",
        _ => mode.ToString(),
    };

    public static string Reason(ExecutionReason reason) => reason switch
    {
        ExecutionReason.Signal => "Signal",
        ExecutionReason.StopLoss => "Stop de protection",
        ExecutionReason.TakeProfit => "Prise de bénéfice",
        ExecutionReason.TrailingStop => "Stop suiveur",
        _ => reason.ToString(),
    };

    public static string Percent(decimal fraction, int decimals = 1) =>
        (fraction * 100m).ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), French) + " %";

    public static string SignedPercent(decimal fraction, int decimals = 1) =>
        (fraction > 0m ? "+" : string.Empty) + Percent(fraction, decimals);

    public static string Money(decimal amount, int decimals = 0) =>
        amount.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), French) + " €";

    public static string Number(decimal value, int decimals = 2) =>
        value.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), French);

    /// <summary>Un nombre sans zéros superflus : « 2 », « 0,02 ».</summary>
    public static string Plain(decimal value) => value.ToString("0.############", French);

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", French);
}
