using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class LocalGameTests
{
    private static Game LocalGame(int seed = 6)
    {
        var placer = new RandomFleetPlacer(new Random(seed));
        var first = new Player("Olivier", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var second = new Player("Ulysse", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        return new Game(GameMode.Local, first, second);
    }

    [Fact]
    public void ALocalGame_StartsImmediately_AndDescribesTheFirstPlayer()
    {
        var view = LocalGame().ViewForClient();

        Assert.Equal(GameStatus.InProgress, view.Status);
        Assert.Equal("Olivier", view.ViewerName);
        Assert.True(view.IsViewerTurn);
    }

    [Fact]
    public void FireFromClient_InALocalGame_HandsTheViewToTheOtherHuman()
    {
        var game = LocalGame();

        game.FireFromClient(new Coordinates(0, 0));

        Assert.Equal("Ulysse", game.ViewForClient().ViewerName);
    }

    [Fact]
    public void FireFromClient_InALocalGame_OnHit_LetsTheSameHumanReplay()
    {
        var first = new Player("Olivier", isBot: false,
            Rounds.BoardWith(new ShipPlacement(ShipKind.Destroyer, new Coordinates(0, 0), Orientation.Horizontal)));
        var second = new Player("Ulysse", isBot: false,
            Rounds.BoardWith(new ShipPlacement(ShipKind.Destroyer, new Coordinates(7, 7), Orientation.Horizontal)));
        var game = new Game(GameMode.Local, first, second);

        var shooter = game.CurrentPlayer;
        var outcome = game.FireFromClient(new Coordinates(7, 7));

        Assert.Equal(ShotResult.Hit, outcome.Result);
        Assert.Same(shooter, game.CurrentPlayer);
        Assert.Equal("Olivier", game.ViewForClient().ViewerName);
    }

    /// <summary>
    /// C'est l'invariant de l'ADR 0003 vu sous l'angle du hot-seat : le serveur
    /// ne sert jamais deux flottes à la fois. Le tour de l'un ne peut pas révéler
    /// la flotte de l'autre, quel que soit le nombre de tirs joués.
    /// </summary>
    [Fact]
    public void ViewForClient_InALocalGame_OnlyEverCarriesTheViewerOwnFleet()
    {
        var game = LocalGame();
        var fleets = new Dictionary<string, HashSet<Coordinates>>();

        for (var turn = 0; turn < 30 && game.Status is GameStatus.InProgress; turn++)
        {
            var view = game.ViewForClient();
            var served = view.OwnFleet.SelectMany(ship => ship.Cells).ToHashSet();

            if (fleets.TryGetValue(view.ViewerName, out var known))
            {
                Assert.True(known.SetEquals(served), $"la flotte servie à {view.ViewerName} a changé");
            }
            else
            {
                fleets[view.ViewerName] = served;
            }

            game.FireFromClient(new Coordinates(turn % 10, turn / 10));
        }

        Assert.Equal(2, fleets.Count);
        Assert.False(fleets["Olivier"].SetEquals(fleets["Ulysse"]),
            "les deux flottes sont identiques : le test ne prouverait rien");
    }

    [Fact]
    public void PlayBotTurn_InALocalGame_IsRefused() =>
        Assert.Equal(FireRejection.NotTheBotTurn,
            LocalGame().PlayBotTurn(new RandomBot(new Random(1))).Rejection);

    [Fact]
    public void ALocalGame_PlaysThroughToAWinner()
    {
        var game = LocalGame();
        var nextIndex = new Dictionary<string, int>();

        for (var turn = 0; turn < 400 && game.Status is GameStatus.InProgress; turn++)
        {
            var shooter = game.CurrentPlayer.Name;
            var index = nextIndex.GetValueOrDefault(shooter);
            nextIndex[shooter] = index + 1;

            var cell = new Coordinates(index % 10, (index / 10) % 10);
            game.FireFromClient(cell);
        }

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Contains(game.Winner!.Name, new[] { "Olivier", "Ulysse" });
    }

    /// <summary>
    /// Une partie locale n'oppose aucun bot : la difficulté qu'elle porte est
    /// sans objet, et aucun code ne doit s'en servir. Voir ADR 0009.
    /// </summary>
    [Fact]
    public void ALocalGame_CarriesADifficulty_ThatNothingUses()
    {
        var game = LocalGame();

        Assert.Equal(BotDifficulty.Random, game.BotDifficulty);
        Assert.Equal(FireRejection.NotTheBotTurn,
            game.PlayBotTurn(new BotStrategyFactory(new Random(1)).For(game.BotDifficulty)).Rejection);
    }

    /// <summary>
    /// Remarque de la revue de la PR #6. Le front doit pouvoir armer la
    /// passation dès que le tir est accepté, sans attendre la vue suivante :
    /// sinon un rafraîchissement raté laisse la vue périmée du tireur, qui dit
    /// encore « à vous » — et le serveur accepterait ce second tir comme celui
    /// de l'adversaire. Il lui faut donc le nom du suivant, pas sa position.
    /// </summary>
    [Fact]
    public void ViewForClient_NamesTheOpponent_SoTheHandoverNeedsNoRoundTrip()
    {
        var game = LocalGame();

        var before = game.ViewForClient();
        Assert.Equal("Olivier", before.ViewerName);
        Assert.Equal("Ulysse", before.OpponentName);

        game.FireFromClient(new Coordinates(0, 0));

        var after = game.ViewForClient();
        Assert.Equal("Ulysse", after.ViewerName);
        Assert.Equal("Olivier", after.OpponentName);
    }

    [Fact]
    public void ViewForClient_InASoloGame_NamesTheBotAsOpponent()
    {
        var placer = new RandomFleetPlacer(new Random(3));
        var human = new Player("Olivier", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var bot = new Player("Bot", isBot: true, placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        Assert.Equal("Bot", new Game(GameMode.Solo, human, bot).ViewForClient().OpponentName);
    }
}
