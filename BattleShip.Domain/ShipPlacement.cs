namespace BattleShip.Domain;

public readonly record struct ShipPlacement(ShipKind Kind, Coordinates Origin, Orientation Orientation)
{
    public IReadOnlyList<Coordinates> Cells()
    {
        var size = Kind.Size();
        var cells = new Coordinates[size];

        for (var offset = 0; offset < size; offset++)
        {
            cells[offset] = Orientation is Orientation.Horizontal
                ? Origin with { Column = Origin.Column + offset }
                : Origin with { Row = Origin.Row + offset };
        }

        return cells;
    }
}
