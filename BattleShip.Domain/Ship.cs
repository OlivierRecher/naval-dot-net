namespace BattleShip.Domain;

public sealed class Ship
{
    private readonly HashSet<Coordinates> _hits = [];

    internal Ship(ShipPlacement placement)
    {
        Placement = placement;
        Cells = placement.Cells();
    }

    public ShipPlacement Placement { get; }

    public ShipKind Kind => Placement.Kind;

    public IReadOnlyList<Coordinates> Cells { get; }

    public IReadOnlyCollection<Coordinates> Hits => _hits;

    public bool IsSunk => _hits.Count == Cells.Count;

    internal bool Occupies(Coordinates cell) => Cells.Contains(cell);

    internal void RecordHit(Coordinates cell) => _hits.Add(cell);
}
