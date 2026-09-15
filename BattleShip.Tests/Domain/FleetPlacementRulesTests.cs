using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class FleetPlacementRulesTests
{
    private static readonly BoardSize Size = BoardSize.Standard;

    /// <summary>
    /// Flotte standard valide : cinq navires en colonnes séparées, donc jamais
    /// en chevauchement quelles que soient leurs longueurs.
    /// </summary>
    private static List<ShipPlacement> ValidFleet() =>
    [
        new(ShipKind.Carrier, new Coordinates(0, 0), Orientation.Vertical),
        new(ShipKind.Battleship, new Coordinates(2, 0), Orientation.Vertical),
        new(ShipKind.Cruiser, new Coordinates(4, 0), Orientation.Vertical),
        new(ShipKind.Submarine, new Coordinates(6, 0), Orientation.Vertical),
        new(ShipKind.Destroyer, new Coordinates(8, 0), Orientation.Vertical)
    ];

    private static FleetRejection? Validate(IReadOnlyList<ShipPlacement> fleet) =>
        FleetPlacementRules.Validate(Size, FleetTemplate.Standard, fleet);

    [Fact]
    public void Validate_OnAWellFormedFleet_Accepts() =>
        Assert.Null(Validate(ValidFleet()));

    [Fact]
    public void Validate_WhenANavireIsMissing_RejectsTheComposition()
    {
        var fleet = ValidFleet();
        fleet.RemoveAt(0);

        Assert.Equal(FleetRejection.WrongComposition, Validate(fleet));
    }

    [Fact]
    public void Validate_WhenAKindIsPlacedTwice_RejectsTheComposition()
    {
        var fleet = ValidFleet();
        fleet[0] = fleet[0] with { Kind = ShipKind.Destroyer };

        Assert.Equal(FleetRejection.WrongComposition, Validate(fleet));
    }

    [Fact]
    public void Validate_WhenAShipLeavesTheBoard_RejectsTheBounds()
    {
        var fleet = ValidFleet();
        fleet[0] = fleet[0] with { Origin = new Coordinates(0, Size.Rows - 1) };

        Assert.Equal(FleetRejection.OutOfBounds, Validate(fleet));
    }

    [Fact]
    public void Validate_WhenTwoShipsShareACell_RejectsTheOverlap()
    {
        var fleet = ValidFleet();
        fleet[1] = fleet[1] with { Origin = new Coordinates(0, 0) };

        Assert.Equal(FleetRejection.Overlap, Validate(fleet));
    }

    /// <summary>
    /// Le contact est autorisé par les règles du projet (AGENTS.md § 3) : deux
    /// navires bord à bord ne se chevauchent pas.
    /// </summary>
    [Fact]
    public void Validate_WhenTwoShipsTouchWithoutSharingACell_Accepts()
    {
        var fleet = ValidFleet();
        fleet[1] = fleet[1] with { Origin = new Coordinates(1, 0) };

        Assert.Null(Validate(fleet));
    }

    [Fact]
    public void Validate_IsAPureFunction_AndNeedsNoGame()
    {
        var fleet = ValidFleet();

        Assert.Null(Validate(fleet));
        Assert.Null(Validate(fleet));
    }

    [Fact]
    public void Validate_OnAnEmptyFleet_RejectsTheComposition() =>
        Assert.Equal(FleetRejection.WrongComposition, Validate([]));
}
