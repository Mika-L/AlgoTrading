using AlgoTrading.Domain.MarketData;
using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Domain.Backtesting;

/// <summary>Quantité décidée pour un candidat, à l'issue de l'allocation.</summary>
public readonly record struct Allocation(Symbol Symbol, int Quantity, decimal Strength);

public interface ICapitalAllocator
{
    IReadOnlyList<Allocation> Allocate(IReadOnlyList<Candidate> candidates, decimal equity, decimal cash, PositionSizing sizing, ICostModel costs);
}

/// <summary>
/// Répartit les liquidités entre <b>tous</b> les candidats du jour, collectés avant la
/// moindre exécution.
/// <para>Deux défauts disparaissent ici. Le premier est le biais d'ordre : la première valeur
/// acheteuse de la boucle consommait <c>Cash × 0,1</c> avant que les suivantes ne soient
/// même examinées, et l'ordre en question était celui d'énumération d'un dictionnaire à clés
/// par référence. Le second est la dépendance au chemin : la taille de position se calculait
/// sur le cash <b>résiduel</b> et non sur la valeur du portefeuille, si bien que le montant
/// investi dépendait de ce qui avait déjà été acheté le même jour.</para>
/// </summary>
public sealed class ProRataAllocator : ICapitalAllocator
{
    public static ProRataAllocator Instance { get; } = new();

    public IReadOnlyList<Allocation> Allocate(
        IReadOnlyList<Candidate> candidates,
        decimal equity,
        decimal cash,
        PositionSizing sizing,
        ICostModel costs)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(sizing);
        ArgumentNullException.ThrowIfNull(costs);

        if (candidates.Count == 0 || cash <= 0m)
        {
            return [];
        }

        var target = sizing.Mode == SizingMode.EquityFraction ? equity * sizing.Value : sizing.Value;
        if (target <= 0m)
        {
            return [];
        }

        // Tri déterministe : conviction décroissante, puis symbole. Deux univers déclarés
        // dans un ordre différent produisent exactement la même courbe d'actif.
        var ordered = candidates
            .OrderByDescending(static c => c.Strength)
            .ThenBy(static c => c.Symbol)
            .ToArray();

        var wanted = ordered.Length * target;

        // Si l'ambition dépasse les liquidités, toutes les cibles sont réduites du même
        // facteur — personne n'est servi au détriment des suivants.
        var scale = wanted > cash ? cash / wanted : 1m;

        var allocations = new List<Allocation>(ordered.Length);
        var remaining = cash;

        foreach (var candidate in ordered)
        {
            if (candidate.ReferencePrice <= 0m)
            {
                continue;
            }

            var budget = target * scale;
            var unitCost = costs.ExecutionPrice(candidate.ReferencePrice, OrderSide.Buy);
            var quantity = (int)decimal.Floor(budget / unitCost);

            if (quantity <= 0)
            {
                continue;
            }

            // L'arrondi à l'entier peut faire dépasser : on retaille sur ce qui reste.
            var needed = (quantity * unitCost) + costs.Commission(quantity * unitCost);
            while (quantity > 0 && needed > remaining)
            {
                quantity--;
                needed = (quantity * unitCost) + costs.Commission(quantity * unitCost);
            }

            if (quantity <= 0)
            {
                continue;
            }

            remaining -= needed;
            allocations.Add(new Allocation(candidate.Symbol, quantity, candidate.Strength));
        }

        return allocations;
    }
}
