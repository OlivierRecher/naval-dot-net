namespace BattleShip.Models;

/// <summary>
/// Le niveau du bot voyage en texte et non en énumération : une valeur inconnue
/// doit produire un 400 de FluentValidation, pas une erreur de désérialisation
/// avant que le filtre de validation ait pu s'exécuter. Voir ADR 0009.
/// </summary>
public sealed record CreateGameRequest(
    string PlayerName,
    int Columns,
    int Rows,
    string Mode,
    string BotDifficulty,
    string FleetPlacement,
    string? OpponentName = null);

public sealed record FireRequest(int Column, int Row);

public sealed record ShipPlacementDto(string Kind, int Column, int Row, string Orientation);

public sealed record PlaceFleetRequest(IReadOnlyList<ShipPlacementDto> Ships);

/// <summary>
/// Un navire que le joueur doit encore poser. Le serveur dicte la composition :
/// le front n'a aucune règle de jeu à connaître, et la flotte personnalisable
/// (item 6) ne demandera aucun changement côté client.
/// </summary>
public sealed record ShipToPlaceDto(string Kind, int Size);

public sealed record GameSummaryResponse(
    Guid GameId,
    string Mode,
    string BotDifficulty,
    string Status,
    string FirstPlayer,
    string SecondPlayer,
    string? Winner,
    int Shots,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

/// <summary>
/// Les compteurs bruts, et la précision calculée à partir d'eux. Aucun taux
/// n'est stocké : un total et sa moyenne ne peuvent pas diverger s'il n'y en a
/// qu'un des deux.
/// </summary>
public sealed record StatisticsResponse(
    int Games,
    int Finished,
    int Shots,
    int Hits,
    int Sunk,
    double Accuracy);

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
    string OpponentName,
    bool IsViewerTurn,
    IReadOnlyList<ShipDto> OwnFleet,
    IReadOnlyList<CoordinatesDto> ShotsReceived,
    IReadOnlyList<RevealedCellDto> ShotsFired,
    string BotDifficulty,
    IReadOnlyList<ShipToPlaceDto> FleetToPlace,
    string? Winner);

public sealed record ShotOutcomeResponse(
    CoordinatesDto Target,
    string Result,
    bool GameOver,
    string? Winner,
    GameViewResponse View);

/// <summary>
/// Catalogue des niveaux proposés au joueur. Il vit dans la bibliothèque
/// partagée parce que c'est l'API qui accepte les noms et le front qui les
/// propose : le test <c>BotDifficultyEndpointsTests</c> interdit qu'ils divergent de
/// l'énumération du domaine.
/// </summary>
public sealed record BotDifficultyOption(string Name, string Label, string Summary);

public static class BotDifficultyCatalog
{
    public static IReadOnlyList<BotDifficultyOption> All { get; } =
    [
        new("Random", "Novice", "Tire au hasard sur les cases encore libres."),
        new("HuntTarget", "Chasseur", "Traque un navire touché jusqu'à le couler."),
        new("HuntTargetParity", "Vétéran", "Traque, et balaie la grille en damier.")
    ];

    public static string LabelOf(string? name) =>
        All.FirstOrDefault(option => option.Name == name)?.Label ?? name ?? string.Empty;

    public static string SummaryOf(string? name) =>
        All.FirstOrDefault(option => option.Name == name)?.Summary ?? string.Empty;
}
