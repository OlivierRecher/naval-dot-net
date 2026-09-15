namespace BattleShip.Domain;

public enum ShipKind
{
    Carrier,
    Battleship,
    Cruiser,
    Submarine,
    Destroyer,

    /// <summary>Une seule case. Voir ADR 0013 : il invalide le damier de pas 2.</summary>
    PatrolBoat
}

public static class ShipKindExtensions
{
    public static int Size(this ShipKind kind) => kind switch
    {
        ShipKind.Carrier => 5,
        ShipKind.Battleship => 4,
        ShipKind.Cruiser => 3,
        ShipKind.Submarine => 3,
        ShipKind.Destroyer => 2,
        ShipKind.PatrolBoat => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
