namespace BattleShip.Models;

public sealed record CreateGameRequest(string PlayerName, int Columns, int Rows);

public sealed record FireRequest(int Column, int Row);

public sealed record CoordinatesDto(int Column, int Row);

public sealed record ShipDto(
    string Kind,
    IReadOnlyList<CoordinatesDto> Cells,
    IReadOnlyList<CoordinatesDto> Hits,
    bool IsSunk);

public sealed record RevealedCellDto(CoordinatesDto Target, string Result);

/// <summary>
/// Ce que le serveur accepte de publier. Aucun membre ne peut porter la flotte
/// adverse. Voir ADR 0003.
/// </summary>
public sealed record GameViewResponse(
    Guid GameId,
    string Status,
    string Mode,
    int Columns,
    int Rows,
    string ViewerName,
    bool IsViewerTurn,
    IReadOnlyList<ShipDto> OwnFleet,
    IReadOnlyList<CoordinatesDto> ShotsReceived,
    IReadOnlyList<RevealedCellDto> ShotsFired,
    string? Winner);

public sealed record ShotOutcomeResponse(
    CoordinatesDto Target,
    string Result,
    bool GameOver,
    string? Winner,
    GameViewResponse View);
