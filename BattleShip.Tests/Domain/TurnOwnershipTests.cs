using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

/// <summary>
/// Le client n'envoie aucune identite : c'est le serveur qui decide a qui
/// appartient le tour. Sans ces garde-fous, un second appel du navigateur
/// ferait tirer le bot sur une cible choisie par le client. Voir ADR 0003.
/// </summary>
public class TurnOwnershipTests
{
    private static Game SoloGameWhereTheHumanOpens()
    {
        var placer = new RandomFleetPlacer(new Random(17));
        var human = new Player("Humain", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var bot = new Player("Bot", isBot: true, placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        return new Game(GameMode.Solo, human, bot);
    }

    [Fact]
    public void FireFromClient_WhileItIsTheHumanTurn_IsAccepted()
    {
        var game = SoloGameWhereTheHumanOpens();

        var outcome = game.FireFromClient(new Coordinates(3, 3));

        Assert.True(outcome.IsAccepted);
        Assert.Equal(new Coordinates(3, 3), outcome.Target);
    }

    [Fact]
    public void FireFromClient_OnceTheBotIsToPlay_IsRejectedAndConsumesNothing()
    {
        var game = SoloGameWhereTheHumanOpens();
        game.FireFromClient(new Coordinates(3, 3));

        var outcome = game.FireFromClient(new Coordinates(4, 4));

        Assert.Equal(FireRejection.NotTheClientTurn, outcome.Rejection);
        Assert.Single(game.Shots);
        Assert.True(game.CurrentPlayer.IsBot);
    }

    [Fact]
    public void PlayBotTurn_WhileItIsTheHumanTurn_IsRejected()
    {
        var game = SoloGameWhereTheHumanOpens();

        var outcome = game.PlayBotTurn(new RandomBot(new Random(1)));

        Assert.Equal(FireRejection.NotTheBotTurn, outcome.Rejection);
        Assert.Empty(game.Shots);
    }

    [Fact]
    public void PlayBotTurn_WhenItIsTheBotTurn_FiresOnceAndGivesTheTurnBack()
    {
        var game = SoloGameWhereTheHumanOpens();
        game.FireFromClient(new Coordinates(3, 3));

        var outcome = game.PlayBotTurn(new RandomBot(new Random(1)));

        Assert.True(outcome.IsAccepted);
        Assert.Equal(2, game.Shots.Count);
        Assert.Equal(outcome.Target, game.Shots[^1].Target);
        Assert.False(game.CurrentPlayer.IsBot);
    }

    [Fact]
    public void PlayBotTurn_OnceTheGameIsFinished_IsRejectedWithoutAskingTheStrategy()
    {
        var human = new Player("Humain", isBot: false,
            new RandomFleetPlacer(new Random(2)).Place(new BoardSize(2, 1), [ShipKind.Destroyer]));
        var bot = new Player("Bot", isBot: true,
            new RandomFleetPlacer(new Random(2)).Place(new BoardSize(2, 1), [ShipKind.Destroyer]));

        var game = new Game(GameMode.Solo, human, bot);
        game.FireFromClient(new Coordinates(0, 0));
        game.PlayBotTurn(new RandomBot(new Random(9)));
        game.FireFromClient(new Coordinates(1, 0));

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Equal(FireRejection.GameFinished, game.PlayBotTurn(new RandomBot(new Random(1))).Rejection);
    }

    [Fact]
    public void FireFromClient_InLocalMode_StaysOpenToBothHumans()
    {
        // Le garde vise le bot, pas l'alternance : le hot-seat doit continuer
        // a fonctionner tour a tour.
        var placer = new RandomFleetPlacer(new Random(21));
        var alice = new Player("Alice", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var bob = new Player("Bob", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var game = new Game(GameMode.Local, alice, bob);

        Assert.True(game.FireFromClient(new Coordinates(0, 0)).IsAccepted);
        Assert.True(game.FireFromClient(new Coordinates(1, 1)).IsAccepted);
        Assert.Equal(2, game.Shots.Count);
    }
}
