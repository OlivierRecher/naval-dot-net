using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class BoardTests
{
    private static Board BoardWithOneDestroyerAtOrigin()
    {
        var board = new Board(BoardSize.Standard);
        board.Place(new ShipPlacement(ShipKind.Destroyer, new Coordinates(0, 0), Orientation.Horizontal));
        return board;
    }

    [Fact]
    public void Place_ForAValidPlacement_ReturnsNoErrorAndHoldsTheShip()
    {
        var board = new Board(BoardSize.Standard);

        var error = board.Place(new ShipPlacement(ShipKind.Cruiser, new Coordinates(3, 3), Orientation.Vertical));

        Assert.Null(error);
        Assert.Single(board.Ships);
    }

    [Fact]
    public void Place_WhenTheShipOverlapsAnother_ReturnsOverlapAndLeavesTheBoardUnchanged()
    {
        var board = BoardWithOneDestroyerAtOrigin();

        var error = board.Place(new ShipPlacement(ShipKind.Cruiser, new Coordinates(1, 0), Orientation.Horizontal));

        Assert.Equal(PlacementError.Overlap, error);
        Assert.Single(board.Ships);
    }

    [Fact]
    public void Receive_OnACellHoldingNoShip_ReturnsMiss()
    {
        var board = BoardWithOneDestroyerAtOrigin();

        Assert.Equal(ShotResult.Miss, board.Receive(new Coordinates(5, 5)));
    }

    [Fact]
    public void Receive_OnACellHoldingAShip_ReturnsHit()
    {
        var board = BoardWithOneDestroyerAtOrigin();

        Assert.Equal(ShotResult.Hit, board.Receive(new Coordinates(0, 0)));
    }

    [Fact]
    public void Receive_WhileOneCellOfTheShipStaysIntact_DoesNotReportSunk()
    {
        var board = new Board(BoardSize.Standard);
        board.Place(new ShipPlacement(ShipKind.Cruiser, new Coordinates(0, 0), Orientation.Horizontal));

        board.Receive(new Coordinates(0, 0));

        Assert.Equal(ShotResult.Hit, board.Receive(new Coordinates(1, 0)));
    }

    [Fact]
    public void Receive_OnTheLastIntactCellOfAShip_ReturnsSunk()
    {
        var board = BoardWithOneDestroyerAtOrigin();
        board.Receive(new Coordinates(0, 0));

        Assert.Equal(ShotResult.Sunk, board.Receive(new Coordinates(1, 0)));
    }

    [Fact]
    public void WasAlreadyTargeted_AfterAShotOnThatCell_IsTrue()
    {
        var board = BoardWithOneDestroyerAtOrigin();

        Assert.False(board.WasAlreadyTargeted(new Coordinates(4, 4)));
        board.Receive(new Coordinates(4, 4));
        Assert.True(board.WasAlreadyTargeted(new Coordinates(4, 4)));
    }

    [Fact]
    public void AllShipsSunk_WhileOneShipCellStaysIntact_IsFalse()
    {
        var board = BoardWithOneDestroyerAtOrigin();

        board.Receive(new Coordinates(0, 0));

        Assert.False(board.AllShipsSunk);
    }

    [Fact]
    public void AllShipsSunk_WhenEveryShipCellHasBeenHit_IsTrue()
    {
        var board = BoardWithOneDestroyerAtOrigin();

        board.Receive(new Coordinates(0, 0));
        board.Receive(new Coordinates(1, 0));

        Assert.True(board.AllShipsSunk);
    }

    [Fact]
    public void AllShipsSunk_OnABoardWithoutAnyShip_IsFalse()
    {
        // Une grille vide n'est pas une grille vaincue : sans ce cas, une partie
        // dont les flottes ne sont pas encore posees serait declaree terminee.
        var board = new Board(BoardSize.Standard);

        Assert.False(board.AllShipsSunk);
    }
}
