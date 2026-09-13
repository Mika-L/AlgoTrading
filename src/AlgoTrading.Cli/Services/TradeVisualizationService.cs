using ScottPlot;
using ScottPlot.Plottables;

public class TradeVisualizationService
{
    public void GenerateTradeChart(List<Trade> trades, string outputPath = "trades_chart.png")
    {
        var plot = new Plot();

        // Séparer les trades positifs et négatifs
        var positiveTrades = trades.Where(t => t.IsPositive).ToList();
        var negativeTrades = trades.Where(t => !t.IsPositive).ToList();

        // Préparer les données pour le graphique
        var allDates = trades.Select(t => t.CloseDate).OrderBy(d => d).ToList();
        var positiveValues = new List<double>();
        var negativeValues = new List<double>();
        var dateDoubles = new List<double>();

        foreach (var date in allDates)
        {
            dateDoubles.Add(date.ToOADate());

            var positiveSum = positiveTrades
                .Where(t => t.CloseDate.Date == date.Date)
                .Sum(t => (double)t.ProfitLoss);

            var negativeSum = negativeTrades
                .Where(t => t.CloseDate.Date == date.Date)
                .Sum(t => (double)t.ProfitLoss);

            positiveValues.Add(positiveSum);
            negativeValues.Add(negativeSum);
        }

        // Créer les barres pour les profits (vert)
        if (positiveValues.Any(v => v > 0))
        {
            var profitBar = plot.Add.Bars(dateDoubles.ToArray(), positiveValues.ToArray());
            profitBar.Color = ScottPlot.Colors.Green;
            profitBar.Label = "Trades Positifs";
        }

        // Créer les barres pour les pertes (rouge)
        if (negativeValues.Any(v => v < 0))
        {
            var lossBar = plot.Add.Bars(dateDoubles.ToArray(), negativeValues.ToArray());
            lossBar.Color = ScottPlot.Colors.Red;
            lossBar.Label = "Trades Négatifs";
        }

        // Configuration du graphique
        plot.Title("Analyse des Trades - Profits vs Pertes");
        plot.XLabel("Date");
        plot.YLabel("Profit/Perte (€)");

        // Formatage de l'axe des dates
        plot.Axes.DateTimeTicksBottom();

        // Ajouter une ligne de référence à zéro
        plot.Add.HorizontalLine(0, color: ScottPlot.Colors.Black, width: 1);

        // Légende
        plot.ShowLegend();

        // Sauvegarder le graphique
        plot.Save(outputPath, 800, 600);

        Console.WriteLine($"Graphique sauvegardé : {outputPath}");
    }

    public void GenerateCumulativeChart(List<Trade> trades, string outputPath = "cumulative_trades.png")
    {
        var plot = new Plot();

        // Trier les trades par date de clôture
        var sortedTrades = trades.OrderBy(t => t.CloseDate).ToList();

        // Calculer le profit/perte cumulé
        var cumulativeProfit = 0.0;
        var dates = new List<double>();
        var cumulativeValues = new List<double>();

        foreach (var trade in sortedTrades)
        {
            cumulativeProfit += (double)trade.ProfitLoss;
            dates.Add(trade.CloseDate.ToOADate());
            cumulativeValues.Add(cumulativeProfit);
        }

        // Créer la ligne de profit cumulé
        var line = plot.Add.ScatterLine(xs: dates.ToArray(), ys: cumulativeValues.ToArray());
        line.Color = cumulativeProfit >= 0 ? ScottPlot.Colors.Green : ScottPlot.Colors.Red;
        line.LineWidth = 2;
        line.Label = "Profit/Perte Cumulé";

        // Configuration du graphique
        plot.Title("Performance Cumulative des Trades");
        plot.XLabel("Date");
        plot.YLabel("Profit/Perte Cumulé (€)");

        // Formatage de l'axe des dates
        plot.Axes.DateTimeTicksBottom();

        // Ajouter une ligne de référence à zéro
        plot.Add.HorizontalLine(0, color: ScottPlot.Colors.Black, width: 1);

        // Légende
        plot.ShowLegend();

        // Sauvegarder le graphique
        plot.Save(outputPath, 800, 600);

        Console.WriteLine($"Graphique cumulatif sauvegardé : {outputPath}");
    }

    public void PrintTradesSummary(List<Trade> trades)
    {
        var positiveTrades = trades.Where(t => t.IsPositive).ToList();
        var negativeTrades = trades.Where(t => !t.IsPositive).ToList();

        var totalProfit = positiveTrades.Sum(t => t.ProfitLoss);
        var totalLoss = negativeTrades.Sum(t => t.ProfitLoss);
        var netProfit = totalProfit + totalLoss;

        Console.WriteLine("\n=== RÉSUMÉ DES TRADES ===");
        Console.WriteLine($"Nombre total de trades: {trades.Count}");
        Console.WriteLine($"Trades positifs: {positiveTrades.Count}");
        Console.WriteLine($"Trades négatifs: {negativeTrades.Count}");
        Console.WriteLine($"Profit total: {totalProfit:C}");
        Console.WriteLine($"Perte totale: {totalLoss:C}");
        Console.WriteLine($"Profit net: {netProfit:C}");
        Console.WriteLine($"Taux de réussite: {(double)positiveTrades.Count / trades.Count * 100:F1}%");

        if (positiveTrades.Any())
            Console.WriteLine($"Profit moyen par trade gagnant: {totalProfit / positiveTrades.Count:C}");

        if (negativeTrades.Any())
            Console.WriteLine($"Perte moyenne par trade perdant: {totalLoss / negativeTrades.Count:C}");
    }
}