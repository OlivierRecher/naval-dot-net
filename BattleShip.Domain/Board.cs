namespace BattleShip.Domain;

public sealed class Board(BoardSize size)
{
    private readonly List<Ship> _ships = [];
    private readonly HashSet<Coordinates> _incomingShots = [];

    public BoardSize Size { get; } = size;

    public IReadOnlyList<Ship> Ships => _ships;

    public IReadOnlyCollection<Coordinates> IncomingShots => _incomingShots;

    public bool AllShipsSunk => _ships.Count > 0 && _ships.All(ship => ship.IsSunk);

    /// <summary>
    /// Une grille reconstruite a partir de placements deja decides — relus d'un
    /// stockage, le plus souvent. Un placement refuse leve : une grille a moitie
    /// posee porterait une flotte plus courte que celle qui a ete jouee, donc un
    /// autre vainqueur, sans trace. C'est aussi ce qui permet a <see cref="Place"/>
    /// de rester interne. Voir ADR 0008.
    /// </summary>
    public Board(BoardSize size, IReadOnlyList<ShipPlacement> placements) : this(size)
    {
        foreach (var placement in placements)
        {
            if (Place(placement) is { } error)
            {
                throw new InvalidOperationException(
                    $"Placement incoherent : le navire {placement.Kind} en {placement.Origin} est refuse ({error}).");
            }
        }
    }

    /// <summary>
    /// Interne : hors du domaine, une grille ne se modifie qu'en passant par
    /// <see cref="Game"/>, qui prend son verrou et ecrit au journal. Voir ADR 0008.
    /// </summary>
    internal PlacementError? Place(ShipPlacement placement)
    {
        var error = PlacementRules.Validate(
            Size,
            _ships.Select(ship => ship.Placement).ToList(),
            placement);

        if (error is null)
        {
            _ships.Add(new Ship(placement));
        }

        return error;
    }

    public bool WasAlreadyTargeted(Coordinates target) => _incomingShots.Contains(target);

    /// <summary>
    /// Interne, pour la meme raison que <see cref="Place"/> : un tir pose ici ne
    /// prendrait pas le verrou de la partie, n'irait pas au journal, et ne
    /// mettrait a jour ni le statut ni le vainqueur. Voir ADR 0008.
    /// </summary>
    internal ShotResult Receive(Coordinates target)
    {
        _incomingShots.Add(target);

        var hit = _ships.FirstOrDefault(ship => ship.Occupies(target));
        if (hit is null)
        {
            return ShotResult.Miss;
        }

        hit.RecordHit(target);

        return hit.IsSunk ? ShotResult.Sunk : ShotResult.Hit;
    }
}
