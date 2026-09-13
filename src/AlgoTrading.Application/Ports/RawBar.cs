namespace AlgoTrading.Application.Ports;

/// <summary>
/// Une séance telle qu'elle sort d'une source externe : prix <b>non ajustés</b>, plus la
/// clôture ajustée qui permettra le rétro-ajustement au chargement.
/// <para>Le domaine ne voit jamais ce type : il ne reçoit que des barres déjà ajustées.</para>
/// </summary>
public sealed record RawBar(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal AdjustedClose,
    long Volume);
