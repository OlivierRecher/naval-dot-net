namespace BattleShip.Domain;

public readonly record struct PlayerState(
    Guid Id,
    string Name,
    bool IsBot,
    IReadOnlyList<ShipPlacement> Fleet);

/// <summary>
/// Tout ce qu'un lecteur externe — la persistance — a besoin de savoir, saisi en
/// une fois sous le verrou de l'agrégat. <see cref="First"/> et
/// <see cref="Second"/> sont dans l'ordre d'ouverture, pas dans l'ordre du tour
/// courant : c'est cet ordre-là que le rejeu attend.
/// </summary>
public readonly record struct GameState(
    Guid Id,
    GameMode Mode,
    BotDifficulty BotDifficulty,
    GameStatus Status,
    BoardSize Size,
    string? WinnerName,
    PlayerState First,
    PlayerState Second,
    IReadOnlyList<Shot> Shots);
