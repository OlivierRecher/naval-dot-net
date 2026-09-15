using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class RandomFleetPlacerTests
{
    [Fact]
    public void Place_PutsEveryShipOfTheTemplateOnTheBoard()
    {
        var board = new RandomFleetPlacer(new Random(42)).Place(BoardSize.Standard, FleetTemplate.Standard);

        Assert.Equal(FleetTemplate.Standard.Count, board.Ships.Count);
        Assert.Equal(
            FleetTemplate.Standard.OrderBy(kind => kind),
            board.Ships.Select(ship => ship.Kind).OrderBy(kind => kind));
    }

    [Fact]
    public void Place_GivesEveryShipTheNumberOfCellsOfItsKind()
    {
        var board = new RandomFleetPlacer(new Random(7)).Place(BoardSize.Standard, FleetTemplate.Standard);

        Assert.All(board.Ships, ship => Assert.Equal(ship.Kind.Size(), ship.Cells.Count));
    }

    [Fact]
    public void Place_RepeatedOverManySeeds_NeverOverlapsAndNeverLeavesTheBoard()
    {
        // Un placement aleatoire correct une fois ne prouve rien : le defaut
        // n'apparait que sur la combinaison rare. On balaie donc 200 tirages.
        for (var seed = 0; seed < 200; seed++)
        {
            var board = new RandomFleetPlacer(new Random(seed)).Place(BoardSize.Standard, FleetTemplate.Standard);

            var cells = board.Ships.SelectMany(ship => ship.Cells).ToList();

            Assert.Equal(cells.Count, cells.Distinct().Count());
            Assert.All(cells, cell => Assert.True(BoardSize.Standard.Contains(cell), $"case {cell} hors grille (seed {seed})"));
        }
    }

    [Fact]
    public void Place_WithTheSameSeed_ProducesTheSameBoard()
    {
        var first = new RandomFleetPlacer(new Random(123)).Place(BoardSize.Standard, FleetTemplate.Standard);
        var second = new RandomFleetPlacer(new Random(123)).Place(BoardSize.Standard, FleetTemplate.Standard);

        Assert.Equal(
            first.Ships.Select(ship => ship.Placement),
            second.Ships.Select(ship => ship.Placement));
    }

    [Fact]
    public void Place_OnABoardTooSmallForTheFleet_Throws()
    {
        var placer = new RandomFleetPlacer(new Random(1));

        Assert.Throws<InvalidOperationException>(
            () => placer.Place(new BoardSize(3, 3), FleetTemplate.Standard));
    }
}
