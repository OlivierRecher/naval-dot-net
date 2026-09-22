using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class GameViewForClientTests
{
    private static Game SoloGameWhereTheHumanOpens()
    {
        var placer = new RandomFleetPlacer(new Random(17));
        var human = new Player("Humain", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var bot = new Player("Bot", isBot: true, placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        return new Game(GameMode.Solo, human, bot);
    }

    private static HashSet<Coordinates> FleetOf(Player player) =>
        [.. player.Board.Ships.SelectMany(ship => ship.Cells)];

    /// <summary>
    /// Une case libre de la grille visee : le tir y manque a coup sur, donc la
    /// main passe. Choisir une case en dur ferait dependre le test du tirage du
    /// placeur, sans le dire.
    /// </summary>
    private static Coordinates AnEmptyCellOf(Player player) =>
        Enumerable.Range(0, 100)
            .Select(index => new Coordinates(index % 10, index / 10))
            .First(cell => !FleetOf(player).Contains(cell));

    /// <summary>
    /// L'issue servie a cote de la vue doit decrire la meme partie qu'elle. La
    /// garantie de simultaneite, elle, est structurelle — une seule prise du
    /// verrou — et n'est pas atteignable par un test : voir la PR qui introduit
    /// <see cref="Game.ProjectForClient"/>.
    /// </summary>
    [Fact]
    public void ProjectForClient_OnAFinishedGame_DescribesTheSameGameAsItsView()
    {
        var placement = new ShipPlacement(ShipKind.Destroyer, new Coordinates(0, 0), Orientation.Horizontal);

        var human = new Player("Humain", isBot: false, Rounds.BoardWith(placement));
        var bot = new Player("Bot", isBot: true, Rounds.BoardWith(placement));
        var game = new Game(GameMode.Solo, human, bot);

        game.FireFromClient(new Coordinates(0, 0));
        game.FireFromClient(new Coordinates(1, 0));

        var projection = game.ProjectForClient();

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.True(projection.IsOver);
        Assert.Equal(GameStatus.Finished, projection.View.Status);
        Assert.Equal("Humain", projection.WinnerName);
        Assert.Equal(projection.WinnerName, projection.View.WinnerName);
    }

    [Fact]
    public void ProjectForClient_WhileTheGameRuns_AnnouncesNoWinner()
    {
        var game = SoloGameWhereTheHumanOpens();

        var projection = game.ProjectForClient();

        Assert.False(projection.IsOver);
        Assert.Null(projection.WinnerName);
        Assert.Equal(GameStatus.InProgress, projection.View.Status);
        Assert.Equal(projection.View.WinnerName, projection.WinnerName);
    }

    [Fact]
    public void ViewForClient_WhileItIsTheHumanTurn_DescribesTheHuman()
    {
        var game = SoloGameWhereTheHumanOpens();

        var view = game.ViewForClient();

        Assert.Equal("Humain", view.ViewerName);
        Assert.True(view.IsViewerTurn);
    }

    [Fact]
    public void ViewForClient_OnceTheBotIsToPlay_StillDescribesTheHuman()
    {
        // Une fois la main passee au bot, renvoyer la vue du joueur courant
        // reviendrait a livrer la flotte du bot au navigateur. Voir ADR 0003.
        var game = SoloGameWhereTheHumanOpens();
        var outcome = game.FireFromClient(AnEmptyCellOf(game.Opponent));

        // La main ne passe qu'au coup manque : sans cette garantie, le test
        // affirmerait « une fois au bot de jouer » sans y etre. AGENTS.md § 3.
        Assert.Equal(ShotResult.Miss, outcome.Result);

        var view = game.ViewForClient();

        Assert.Equal("Humain", view.ViewerName);
        Assert.False(view.IsViewerTurn);
    }

    [Fact]
    public void ViewForClient_OnceTheBotIsToPlay_ServesTheHumanFleetAndNotTheBotOne()
    {
        var game = SoloGameWhereTheHumanOpens();
        var humanFleet = FleetOf(game.CurrentPlayer);
        var botFleet = FleetOf(game.Opponent);

        // Les deux flottes occupent des grilles distinctes : partager une
        // coordonnee n'est pas une fuite. Seul le fait que les deux flottes
        // different rend les assertions suivantes discriminantes.
        Assert.False(humanFleet.SetEquals(botFleet), "les deux flottes sont identiques : le test ne prouverait rien");

        var target = AnEmptyCellOf(game.Opponent);

        Assert.Equal(ShotResult.Miss, game.FireFromClient(target).Result);

        var view = game.ViewForClient();
        var served = view.OwnFleet.SelectMany(ship => ship.Cells).ToHashSet();

        Assert.True(served.SetEquals(humanFleet), "la vue servie ne decrit pas la flotte de l'humain");
        Assert.False(served.SetEquals(botFleet), "la vue servie decrit la flotte du bot");
        Assert.All(view.ShotsFired, cell => Assert.Equal(target, cell.Target));
    }

    [Fact]
    public void ViewForClient_InLocalMode_FollowsTheCurrentPlayer()
    {
        // Deux humains : la vue suit bien le tour, c'est le mode hot-seat.
        var placer = new RandomFleetPlacer(new Random(21));
        var first = new Player("Alice", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var second = new Player("Bob", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var game = new Game(GameMode.Local, first, second);

        Assert.Equal("Alice", game.ViewForClient().ViewerName);
        game.FireFromClient(new Coordinates(0, 0));
        Assert.Equal("Bob", game.ViewForClient().ViewerName);
    }
}
