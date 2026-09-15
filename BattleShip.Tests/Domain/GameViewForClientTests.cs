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
        // Apres le tir de l'humain le tour passe au bot. Renvoyer la vue du
        // joueur courant reviendrait alors a livrer la flotte du bot au
        // navigateur. Voir ADR 0003.
        var game = SoloGameWhereTheHumanOpens();
        game.FireFromClient(new Coordinates(0, 0));

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

        game.FireFromClient(new Coordinates(0, 0));
        var view = game.ViewForClient();

        var served = view.OwnFleet.SelectMany(ship => ship.Cells).ToHashSet();

        Assert.True(served.SetEquals(humanFleet), "la vue servie ne decrit pas la flotte de l'humain");
        Assert.False(served.SetEquals(botFleet), "la vue servie decrit la flotte du bot");
        Assert.All(view.ShotsFired, cell => Assert.Equal(new Coordinates(0, 0), cell.Target));
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
