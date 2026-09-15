namespace BattleShip.Domain;

public enum PlacementError
{
    OutOfBounds,
    Overlap
}

public static class PlacementRules
{
    public static PlacementError? Validate(
        BoardSize size,
        IReadOnlyCollection<ShipPlacement> existing,
        ShipPlacement candidate)
    {
        var cells = candidate.Cells();

        if (cells.Any(cell => !size.Contains(cell)))
        {
            return PlacementError.OutOfBounds;
        }

        var occupied = existing.SelectMany(placement => placement.Cells()).ToHashSet();

        return cells.Any(occupied.Contains)
            ? PlacementError.Overlap
            : null;
    }
}
