using AlgoTrading.Domain.Strategies;

namespace AlgoTrading.Application.Ports;

/// <summary>Une stratégie enregistrée, désignée par la clé sous laquelle on la retrouve.</summary>
public sealed record StoredStrategy(string Key, StrategyDefinition Definition);

/// <summary>
/// Bibliothèque de stratégies. La ligne de commande lit des fichiers qu'on lui désigne ;
/// l'interface graphique a besoin de les lister, de les modifier et d'en créer.
/// </summary>
public interface IStrategyRepository
{
    /// <summary>Clés connues, triées. Une stratégie illisible y figure quand même : on doit pouvoir la corriger.</summary>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Texte brut de la stratégie, ou <c>null</c> si la clé est inconnue.</summary>
    Task<string?> ReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Enregistre une stratégie <b>valide</b> et retourne sa clé.</summary>
    Task<string> SaveAsync(string key, StrategyDefinition strategy, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
