namespace BattleShip.Domain;

public readonly record struct ShipView(
    ShipKind Kind,
    IReadOnlyList<Coordinates> Cells,
    IReadOnlyCollection<Coordinates> Hits,
    bool IsSunk);

public readonly record struct RevealedCell(Coordinates Target, ShotResult Result);

public readonly record struct ShipToPlace(ShipKind Kind, int Size);

/// <summary>
/// Tout ce que le serveur sert au navigateur après une transition, pris en
/// <b>une seule fois</b> sous le verrou de la partie. Lire la vue, le statut et
/// le vainqueur séparément laisse la réponse se contredire — l'échange de tour
/// est une affectation de tuple, donc non atomique. Voir ADR 0008 et 0012.
/// </summary>
public sealed record GameProjection(GameView View, bool IsOver, string? WinnerName);

/// <summary>
/// Ce que le viewer — le joueur decrit par la vue — a le droit de voir. Ce
/// n'est pas toujours le joueur courant : <see cref="Game.ViewForClient"/>
/// decrit l'humain meme quand le bot doit jouer. Le type ne porte aucun membre
/// capable de transporter la flotte adverse : c'est la forme qui garantit
/// l'invariant, pas un filtrage. Voir ADR 0003.
/// </summary>
public sealed record GameView(
    Guid GameId,
    GameStatus Status,
    GameMode Mode,
    BoardSize Size,
    string ViewerName,
    string OpponentName,
    bool IsViewerTurn,
    IReadOnlyList<ShipView> OwnFleet,
    IReadOnlyCollection<Coordinates> ShotsReceived,
    IReadOnlyList<RevealedCell> ShotsFired,
    BotDifficulty BotDifficulty,
    IReadOnlyList<ShipToPlace> FleetToPlace,
    /// <summary>
    /// La composition en jeu, des deux côtés. Publique par construction : elle
    /// est annoncée à la création et les deux joueurs partagent la même. Elle ne
    /// dit rien des positions. Le bot en déduit le pas de son balayage.
    /// </summary>
    IReadOnlyList<ShipToPlace> Fleet,
    string? WinnerName);
