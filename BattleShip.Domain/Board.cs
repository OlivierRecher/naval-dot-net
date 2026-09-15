namespace BattleShip.Domain;

public sealed class Board(BoardSize size)
{
    private readonly List<Ship> _ships = [];
    private readonly HashSet<Coordinates> _incomingShots = [];

    public BoardSize Size { get; } = size;

    public IReadOnlyList<Ship> Ships => _ships;

    public IReadOnlyCollection<Coordinates> IncomingShots => _incomingShots;

    public bool AllShipsSunk => _ships.Count > 0 && _ships.All(ship => ship.IsSunk);

    public PlacementError? Place(ShipPlacement placement)
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

    public ShotResult Receive(Coordinates target)
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
