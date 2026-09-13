namespace AlgoTrading.Domain.Strategies;

/// <summary>
/// Direction d'un signal. Un champ d'énumération ne peut pas valoir deux choses à la fois :
/// l'exclusivité entre haussier et baissier n'est plus une règle à respecter mais une
/// propriété du type. Le code d'origine exposait deux booléens indépendants, qui pouvaient
/// tous les deux être vrais.
/// </summary>
public enum SignalDirection
{
    Bearish = -1,
    Neutral = 0,
    Bullish = 1,
}

/// <summary>
/// Nature d'une règle. Un défaut majeur du code d'origine : le MACD émettait un signal
/// <b>chaque jour</b> (état) tandis que la MME n'en émettait qu'aux croisements (événement),
/// et les deux votaient dans la même majorité. Un vote « 2 sur 4 » dont deux règles sont
/// structurellement muettes 99 % du temps ne veut rien dire.
/// </summary>
public enum SignalKind
{
    /// <summary>La règle décrit une situation qui dure — elle se prononce à chaque barre.</summary>
    State,

    /// <summary>La règle décrit un basculement — elle ne se prononce qu'au moment où il survient.</summary>
    Event,
}

/// <summary>
/// Verdict d'une règle sur une barre. <see cref="Strength"/> porte la conviction, entre 0 et 1 :
/// elle sert à l'agrégation pondérée et au tri déterministe des candidats quand le capital
/// ne suffit pas pour tous.
/// </summary>
public readonly record struct Signal
{
    public Signal(SignalDirection direction, decimal strength)
    {
        if (strength is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(strength), strength, "La conviction d'un signal est une fraction entre 0 et 1.");
        }

        Direction = direction;
        Strength = direction == SignalDirection.Neutral ? 0m : strength;
    }

    public SignalDirection Direction { get; }

    public decimal Strength { get; }

    public bool IsBullish => Direction == SignalDirection.Bullish;

    public bool IsBearish => Direction == SignalDirection.Bearish;

    public bool IsNeutral => Direction == SignalDirection.Neutral;

    public static Signal None { get; } = new(SignalDirection.Neutral, 0m);

    public static Signal Bullish(decimal strength = 1m) => new(SignalDirection.Bullish, strength);

    public static Signal Bearish(decimal strength = 1m) => new(SignalDirection.Bearish, strength);

    public static Signal Of(SignalDirection direction, decimal strength = 1m) => new(direction, strength);

    public override string ToString() => IsNeutral ? "Neutre" : $"{Direction} ({Strength:P0})";
}
