namespace BattleShip.Domain;

public readonly record struct ShipView(
    ShipKind Kind,
    IReadOnlyList<Coordinates> Cells,
    IReadOnlyCollection<Coordinates> Hits,
    bool IsSunk);

public readonly record struct RevealedCell(Coordinates Target, ShotResult Result);

/// <summary>
/// Ce que le joueur courant a le droit de voir. Le type ne porte aucun membre
/// capable de transporter la flotte adverse : c'est la forme qui garantit
/// l'invariant, pas un filtrage. Voir ADR 0003.
/// </summary>
public sealed record GameView(
    Guid GameId,
    GameStatus Status,
    GameMode Mode,
    BoardSize Size,
    string ViewerName,
    bool IsViewerTurn,
    IReadOnlyList<ShipView> OwnFleet,
    IReadOnlyCollection<Coordinates> ShotsReceived,
    IReadOnlyList<RevealedCell> ShotsFired,
    string? WinnerName);
