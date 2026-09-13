using System;
using ConsoleApp1.Models;

namespace ConsoleApp1.Indicators;

/*
✅ MME 9 & 20 → Détecter les tendances rapides.
✅ VWAP → Identifier les niveaux clés des institutions.
✅ RSI → Repérer les excès de marché.
✅ MACD → Confirmer la direction du mouvement.
✅ Bandes de Bollinger → Détecter la volatilité et les breakouts.
✅ Pivot Points / Fibonacci → Définir les niveaux de support et résistance.
✅ Volume → Vérifier la force des mouvements.
Conclusion :

En day trading, il est crucial de combiner plusieurs indicateurs pour éviter les faux signaux. Une approche basée sur la tendance (MME, VWAP), les niveaux clés (Pivot Points, Fibonacci) et la volatilité (Bandes de Bollinger, Volume) est la plus efficace pour prendre des décisions rapides et précises.
*/
public interface IIndicator
{
    bool IsBullish(DateTime date);

    bool IsBearish(DateTime date);

    decimal GetPrice(DateTime date);
}
