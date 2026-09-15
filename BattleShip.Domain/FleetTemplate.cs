namespace BattleShip.Domain;

public static class FleetTemplate
{
    public static IReadOnlyList<ShipKind> Standard { get; } =
    [
        ShipKind.Carrier,
        ShipKind.Battleship,
        ShipKind.Cruiser,
        ShipKind.Submarine,
        ShipKind.Destroyer
    ];
}
