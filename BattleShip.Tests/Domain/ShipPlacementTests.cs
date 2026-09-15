using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class ShipPlacementTests
{
    [Fact]
    public void Cells_ForHorizontalPlacement_SpanColumnsFromTheOrigin()
    {
        var placement = new ShipPlacement(ShipKind.Destroyer, new Coordinates(2, 3), Orientation.Horizontal);

        Assert.Equal(
            [new Coordinates(2, 3), new Coordinates(3, 3)],
            placement.Cells());
    }

    [Fact]
    public void Cells_ForVerticalPlacement_SpanRowsFromTheOrigin()
    {
        var placement = new ShipPlacement(ShipKind.Destroyer, new Coordinates(2, 3), Orientation.Vertical);

        Assert.Equal(
            [new Coordinates(2, 3), new Coordinates(2, 4)],
            placement.Cells());
    }

    [Theory]
    [InlineData(ShipKind.Carrier, 5)]
    [InlineData(ShipKind.Battleship, 4)]
    [InlineData(ShipKind.Cruiser, 3)]
    [InlineData(ShipKind.Submarine, 3)]
    [InlineData(ShipKind.Destroyer, 2)]
    public void Cells_Count_MatchesTheSizeOfTheShipKind(ShipKind kind, int expectedSize)
    {
        var placement = new ShipPlacement(kind, new Coordinates(0, 0), Orientation.Horizontal);

        Assert.Equal(expectedSize, placement.Cells().Count);
    }
}
