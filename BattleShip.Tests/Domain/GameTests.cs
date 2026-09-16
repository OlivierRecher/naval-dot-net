using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class GameTests
{
    /// <summary>
    /// Chaque joueur possede un unique torpilleur, sur des cases distinctes des
    /// deux grilles, pour qu'un tir puisse etre dirige vers un navire connu.
    /// </summary>
    private static Game DuelOfTwoDestroyers()
    {
        var challenger = new Player("Challenger", isBot: false,
            Rounds.BoardWith(new ShipPlacement(ShipKind.Destroyer, new Coordinates(0, 0), Orientation.Horizontal)));

        var defender = new Player("Defender", isBot: true,
            Rounds.BoardWith(new ShipPlacement(ShipKind.Destroyer, new Coordinates(7, 7), Orientation.Horizontal)));

        return new Game(GameMode.Solo, challenger, defender);
    }

    [Fact]
    public void Fire_OnAnEmptyCellOfTheOpponentBoard_ReturnsMiss()
    {
        var game = DuelOfTwoDestroyers();

        var outcome = game.Fire(new Coordinates(3, 3));

        Assert.True(outcome.IsAccepted);
        Assert.Equal(ShotResult.Miss, outcome.Result);
    }

    [Fact]
    public void Fire_OnACellHoldingAnOpponentShip_ReturnsHit()
    {
        var game = DuelOfTwoDestroyers();

        var outcome = game.Fire(new Coordinates(7, 7));

        Assert.Equal(ShotResult.Hit, outcome.Result);
    }

    [Fact]
    public void Fire_OnAHit_KeepsTheTurn()
    {
        var game = DuelOfTwoDestroyers();
        var shooter = game.CurrentPlayer;

        game.Fire(new Coordinates(7, 7));

        Assert.Same(shooter, game.CurrentPlayer);
    }

    [Fact]
    public void Fire_OnAMiss_PassesTheTurn()
    {
        var game = DuelOfTwoDestroyers();
        var shooter = game.CurrentPlayer;

        game.Fire(new Coordinates(3, 3));

        Assert.NotSame(shooter, game.CurrentPlayer);
    }

    [Fact]
    public void Fire_OnACellOutsideTheBoard_IsRejected()
    {
        var game = DuelOfTwoDestroyers();

        var outcome = game.Fire(new Coordinates(10, 0));

        Assert.Equal(FireRejection.OutsideBoard, outcome.Rejection);
    }

    [Fact]
    public void Fire_OnACellAlreadyTargeted_IsRejected()
    {
        var game = DuelOfTwoDestroyers();
        game.Fire(new Coordinates(3, 3));
        game.Fire(new Coordinates(4, 4));

        var outcome = game.Fire(new Coordinates(3, 3));

        Assert.Equal(FireRejection.AlreadyTargeted, outcome.Rejection);
    }

    [Fact]
    public void Fire_OnACellAlreadyTargeted_DoesNotConsumeTheTurn()
    {
        var game = DuelOfTwoDestroyers();
        game.Fire(new Coordinates(3, 3));
        game.Fire(new Coordinates(4, 4));
        var shooter = game.CurrentPlayer;

        game.Fire(new Coordinates(3, 3));

        Assert.Same(shooter, game.CurrentPlayer);
    }

    [Fact]
    public void Fire_OnACellAlreadyTargeted_AppendsNothingToTheJournal()
    {
        // Invariant de l'ADR 0002 : un tir accepte et une entree au journal sont
        // une seule et meme chose.
        var game = DuelOfTwoDestroyers();
        game.Fire(new Coordinates(3, 3));
        game.Fire(new Coordinates(4, 4));
        var journalLength = game.Shots.Count;

        game.Fire(new Coordinates(3, 3));

        Assert.Equal(journalLength, game.Shots.Count);
    }

    [Fact]
    public void Shots_AfterAnAcceptedShot_RecordsTheShooterTheTargetAndTheResult()
    {
        var game = DuelOfTwoDestroyers();
        var shooter = game.CurrentPlayer;

        game.Fire(new Coordinates(7, 7));

        var shot = Assert.Single(game.Shots);
        Assert.Equal(shooter.Id, shot.ShooterId);
        Assert.Equal(new Coordinates(7, 7), shot.Target);
        Assert.Equal(ShotResult.Hit, shot.Result);
    }

    [Fact]
    public void Fire_WhenTheLastOpponentShipIsSunk_FinishesTheGameAndNamesTheWinner()
    {
        var game = DuelOfTwoDestroyers();
        var challenger = game.CurrentPlayer;

        game.Fire(new Coordinates(7, 7));   // challenger touche
        var outcome = game.Fire(new Coordinates(8, 7));   // challenger coule

        Assert.Equal(ShotResult.Sunk, outcome.Result);
        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Same(challenger, game.Winner);
    }

    [Fact]
    public void Fire_AfterTheGameIsFinished_IsRejected()
    {
        var game = DuelOfTwoDestroyers();
        game.Fire(new Coordinates(7, 7));
        game.Fire(new Coordinates(8, 7));

        var outcome = game.Fire(new Coordinates(1, 1));

        Assert.Equal(FireRejection.GameFinished, outcome.Rejection);
    }

    [Fact]
    public void ViewFor_RevealsTheOwnFleet()
    {
        var game = DuelOfTwoDestroyers();

        var view = game.ViewFor(game.CurrentPlayer);

        var ownCells = view.OwnFleet.SelectMany(ship => ship.Cells).ToHashSet();
        Assert.Contains(new Coordinates(0, 0), ownCells);
        Assert.Contains(new Coordinates(1, 0), ownCells);
    }

    [Fact]
    public void ViewFor_DoesNotRevealAnUntouchedOpponentShip()
    {
        // Invariant de l'ADR 0003 : la vue ne porte sur l'adversaire que les
        // cases que le joueur a lui-meme visees.
        var game = DuelOfTwoDestroyers();
        game.Fire(new Coordinates(3, 3));   // tir a cote du torpilleur adverse
        game.Fire(new Coordinates(5, 5));   // le defender joue, le tour revient

        var view = game.ViewFor(game.CurrentPlayer);

        var knownAboutOpponent = view.ShotsFired.Select(cell => cell.Target).ToHashSet();
        Assert.DoesNotContain(new Coordinates(7, 7), knownAboutOpponent);
        Assert.DoesNotContain(new Coordinates(8, 7), knownAboutOpponent);
    }

    [Fact]
    public void ViewFor_AfterAnAcceptedShot_DescribesTheOtherPlayer()
    {
        // La bascule de point de vue est pilotee par le serveur, jamais demandee
        // par le client. Voir ADR 0003.
        var game = DuelOfTwoDestroyers();
        var before = game.ViewFor(game.CurrentPlayer).ViewerName;

        game.Fire(new Coordinates(3, 3));

        Assert.NotEqual(before, game.ViewFor(game.CurrentPlayer).ViewerName);
    }

    [Fact]
    public void ViewFor_ReportsTheShotsReceivedOnTheOwnBoard()
    {
        var game = DuelOfTwoDestroyers();
        var challenger = game.CurrentPlayer;
        game.Fire(new Coordinates(3, 3));
        game.Fire(new Coordinates(0, 0));   // le defender touche le challenger

        var view = game.ViewFor(challenger);

        Assert.Contains(new Coordinates(0, 0), view.ShotsReceived);
    }
}
