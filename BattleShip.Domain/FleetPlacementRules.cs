namespace BattleShip.Domain;

public enum FleetRejection
{
    WrongComposition,
    OutOfBounds,
    Overlap,
    FleetAlreadyPlaced
}

public sealed record FleetOutcome(FleetRejection? Rejection)
{
    public bool IsAccepted => Rejection is null;

    public static FleetOutcome Accepted { get; } = new((FleetRejection?)null);

    public static FleetOutcome Rejected(FleetRejection reason) => new(reason);
}

/// <summary>
/// Valide une flotte <b>entière</b> avant qu'aucune case ne soit posée : une
/// flotte refusée ne doit jamais laisser la grille à moitié remplie.
/// </summary>
public static class FleetPlacementRules
{
    public static FleetRejection? Validate(
        BoardSize size,
        IReadOnlyList<ShipKind> template,
        IReadOnlyList<ShipPlacement> placements)
    {
        if (!MatchesComposition(template, placements))
        {
            return FleetRejection.WrongComposition;
        }

        var accepted = new List<ShipPlacement>(placements.Count);

        foreach (var placement in placements)
        {
            if (PlacementRules.Validate(size, accepted, placement) is { } error)
            {
                return error is PlacementError.OutOfBounds
                    ? FleetRejection.OutOfBounds
                    : FleetRejection.Overlap;
            }

            accepted.Add(placement);
        }

        return null;
    }

    private static bool MatchesComposition(
        IReadOnlyList<ShipKind> template,
        IReadOnlyList<ShipPlacement> placements) =>
        placements.Count == template.Count &&
        placements.Select(placement => placement.Kind).Order().SequenceEqual(template.Order());
}
