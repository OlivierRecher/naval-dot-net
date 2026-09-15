using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class PlacementRulesTests
{
    private static readonly BoardSize Standard = new(10, 10);

    [Fact]
    public void Validate_ForAPlacementInsideAnEmptyBoard_ReturnsNoError()
    {
        var candidate = new ShipPlacement(ShipKind.Cruiser, new Coordinates(0, 0), Orientation.Horizontal);

        Assert.Null(PlacementRules.Validate(Standard, [], candidate));
    }

    [Fact]
    public void Validate_WhenTheShipRunsPastTheLastColumn_ReturnsOutOfBounds()
    {
        // Un croiseur de 3 cases posé en colonne 8 occuperait les colonnes 8, 9 et 10.
        var candidate = new ShipPlacement(ShipKind.Cruiser, new Coordinates(8, 0), Orientation.Horizontal);

        Assert.Equal(PlacementError.OutOfBounds, PlacementRules.Validate(Standard, [], candidate));
    }

    [Fact]
    public void Validate_WhenTheShipRunsPastTheLastRow_ReturnsOutOfBounds()
    {
        var candidate = new ShipPlacement(ShipKind.Cruiser, new Coordinates(0, 8), Orientation.Vertical);

        Assert.Equal(PlacementError.OutOfBounds, PlacementRules.Validate(Standard, [], candidate));
    }

    [Fact]
    public void Validate_WhenTheOriginIsOutsideTheBoard_ReturnsOutOfBounds()
    {
        var candidate = new ShipPlacement(ShipKind.Destroyer, new Coordinates(-1, 0), Orientation.Horizontal);

        Assert.Equal(PlacementError.OutOfBounds, PlacementRules.Validate(Standard, [], candidate));
    }

    [Fact]
    public void Validate_WhenTheShipSharesACellWithAnother_ReturnsOverlap()
    {
        ShipPlacement[] existing = [new(ShipKind.Cruiser, new Coordinates(4, 4), Orientation.Horizontal)];
        var candidate = new ShipPlacement(ShipKind.Destroyer, new Coordinates(5, 4), Orientation.Vertical);

        Assert.Equal(PlacementError.Overlap, PlacementRules.Validate(Standard, existing, candidate));
    }

    [Fact]
    public void Validate_WhenTheShipOnlyTouchesAnother_ReturnsNoError()
    {
        // Notre règle autorise le contact : seul le chevauchement est interdit.
        // Voir AGENTS.md § 3.
        ShipPlacement[] existing = [new(ShipKind.Cruiser, new Coordinates(4, 4), Orientation.Horizontal)];
        var candidate = new ShipPlacement(ShipKind.Destroyer, new Coordinates(4, 5), Orientation.Horizontal);

        Assert.Null(PlacementRules.Validate(Standard, existing, candidate));
    }
}
