using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class ManualFleetPlacementTests
{
    private static List<ShipPlacement> ValidFleet() =>
    [
        new(ShipKind.Carrier, new Coordinates(0, 0), Orientation.Vertical),
        new(ShipKind.Battleship, new Coordinates(2, 0), Orientation.Vertical),
        new(ShipKind.Cruiser, new Coordinates(4, 0), Orientation.Vertical),
        new(ShipKind.Submarine, new Coordinates(6, 0), Orientation.Vertical),
        new(ShipKind.Destroyer, new Coordinates(8, 0), Orientation.Vertical)
    ];

    /// <summary>
    /// Partie solo dont l'humain n'a pas encore posé sa flotte : sa grille est
    /// vide, celle du bot est remplie par le serveur.
    /// </summary>
    private static Game AwaitingFleetGame()
    {
        var human = new Player("Humain", isBot: false, new Board(BoardSize.Standard));
        var bot = new Player("Bot", isBot: true,
            new RandomFleetPlacer(new Random(4)).Place(BoardSize.Standard, FleetTemplate.Standard));

        return new Game(GameMode.Solo, human, bot);
    }

    [Fact]
    public void ANewGame_WhoseHumanFleetIsMissing_AwaitsIt() =>
        Assert.Equal(GameStatus.AwaitingFleet, AwaitingFleetGame().Status);

    [Fact]
    public void ANewGame_WhoseBothFleetsArePlaced_StartsImmediately()
    {
        var placer = new RandomFleetPlacer(new Random(4));
        var human = new Player("Humain", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var bot = new Player("Bot", isBot: true, placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        Assert.Equal(GameStatus.InProgress, new Game(GameMode.Solo, human, bot).Status);
    }

    [Fact]
    public void PlaceFleetFromClient_WithAValidFleet_StartsTheGame()
    {
        var game = AwaitingFleetGame();

        var outcome = game.PlaceFleetFromClient(ValidFleet());

        Assert.True(outcome.IsAccepted);
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Equal(5, game.ViewForClient().OwnFleet.Count);
    }

    [Fact]
    public void PlaceFleetFromClient_Twice_IsRefused()
    {
        var game = AwaitingFleetGame();
        game.PlaceFleetFromClient(ValidFleet());

        var outcome = game.PlaceFleetFromClient(ValidFleet());

        Assert.Equal(FleetRejection.FleetAlreadyPlaced, outcome.Rejection);
    }

    /// <summary>
    /// Sans ce contrôle, une flotte refusée pourrait laisser la grille à moitié
    /// remplie : la validation est totale avant que la moindre case soit posée.
    /// </summary>
    [Fact]
    public void PlaceFleetFromClient_WithAnInvalidFleet_LeavesTheBoardUntouched()
    {
        var game = AwaitingFleetGame();
        var fleet = ValidFleet();
        fleet[1] = fleet[1] with { Origin = new Coordinates(0, 0) };

        var outcome = game.PlaceFleetFromClient(fleet);

        Assert.Equal(FleetRejection.Overlap, outcome.Rejection);
        Assert.Equal(GameStatus.AwaitingFleet, game.Status);
        Assert.Empty(game.ViewForClient().OwnFleet);
    }

    [Fact]
    public void FireFromClient_BeforeTheFleetIsPlaced_IsRefused()
    {
        var game = AwaitingFleetGame();

        var outcome = game.FireFromClient(new Coordinates(0, 0));

        Assert.Equal(FireRejection.FleetNotPlaced, outcome.Rejection);
    }

    [Fact]
    public void PlayBotTurn_BeforeTheFleetIsPlaced_IsRefused()
    {
        var game = AwaitingFleetGame();

        var outcome = game.PlayBotTurn(new RandomBot(new Random(1)));

        Assert.Equal(FireRejection.FleetNotPlaced, outcome.Rejection);
    }

    /// <summary>
    /// État dégénéré, inatteignable par l'API — le serveur remplit toujours la
    /// grille du bot — mais c'est le <b>seul</b> scénario qui distingue le garde
    /// « jamais la grille d'un bot » de son absence. Sans lui, retirer
    /// <c>!player.IsBot</c> ne fait échouer aucun test, et le client poserait la
    /// flotte du bot. Voir ADR 0003.
    /// </summary>
    [Fact]
    public void PlaceFleetFromClient_WhenOnlyABotBoardIsEmpty_RefusesRatherThanFillingIt()
    {
        var human = new Player("Humain", isBot: false,
            new RandomFleetPlacer(new Random(4)).Place(BoardSize.Standard, FleetTemplate.Standard));
        var bot = new Player("Bot", isBot: true, new Board(BoardSize.Standard));
        var game = new Game(GameMode.Solo, human, bot);

        var outcome = game.PlaceFleetFromClient(ValidFleet());

        Assert.Equal(FleetRejection.FleetAlreadyPlaced, outcome.Rejection);
        Assert.Empty(game.Opponent.Board.Ships);
    }

    /// <summary>
    /// Le client n'envoie aucune identité : le serveur pose la flotte du seul
    /// humain qui en attend une, jamais celle d'un bot. Voir ADR 0003.
    /// </summary>
    [Fact]
    public void PlaceFleetFromClient_NeverTouchesTheBotBoard()
    {
        var game = AwaitingFleetGame();
        var botCellsBefore = game.Opponent.Board.Ships.SelectMany(ship => ship.Cells).ToHashSet();

        game.PlaceFleetFromClient(ValidFleet());

        var botCellsAfter = game.Opponent.Board.Ships.SelectMany(ship => ship.Cells).ToHashSet();
        Assert.True(botCellsBefore.SetEquals(botCellsAfter));
        Assert.Equal(5, game.Opponent.Board.Ships.Count);
    }

    [Fact]
    public void PlaceFleetFromClient_OnAGameAlreadyRunning_IsRefused()
    {
        var placer = new RandomFleetPlacer(new Random(4));
        var human = new Player("Humain", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var bot = new Player("Bot", isBot: true, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var game = new Game(GameMode.Solo, human, bot);

        Assert.Equal(FleetRejection.FleetAlreadyPlaced, game.PlaceFleetFromClient(ValidFleet()).Rejection);
    }

    [Fact]
    public void AManuallyPlacedGame_PlaysThroughToAWinner()
    {
        var game = AwaitingFleetGame();
        game.PlaceFleetFromClient(ValidFleet());
        var strategy = new BotStrategyFactory(new Random(8)).For(BotDifficulty.HuntTarget);

        foreach (var cell in Enumerable.Range(0, 100).Select(i => new Coordinates(i % 10, i / 10)))
        {
            if (game.Status is GameStatus.Finished) break;
            game.PlayRound(strategy, cell);
        }

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.NotNull(game.Winner);
    }

    /// <summary>
    /// Remarque de la revue de la PR #5. En mode <c>Local</c>, une fois la
    /// première flotte posée, la vue servie doit décrire le <b>second</b> joueur :
    /// sinon il reçoit un écran de placement sans rien à poser, et la partie est
    /// bloquée. Le mode n'est pas encore créable par l'API — c'est l'item 4 —
    /// mais l'agrégat, lui, le construit déjà.
    /// </summary>
    [Fact]
    public void ViewForClient_WhenTheFirstOfTwoHumansHasPlaced_DescribesTheSecond()
    {
        var first = new Player("Olivier", isBot: false, new Board(BoardSize.Standard));
        var second = new Player("Ulysse", isBot: false, new Board(BoardSize.Standard));
        var game = new Game(GameMode.Local, first, second);

        Assert.Equal("Olivier", game.ViewForClient().ViewerName);
        Assert.Equal(5, game.ViewForClient().FleetToPlace.Count);

        game.PlaceFleetFromClient(ValidFleet());

        var view = game.ViewForClient();
        Assert.Equal(GameStatus.AwaitingFleet, game.Status);
        Assert.Equal("Ulysse", view.ViewerName);
        Assert.Equal(5, view.FleetToPlace.Count);
        Assert.Empty(view.OwnFleet);
    }

    [Fact]
    public void PlaceFleetFromClient_WhenBothHumansHavePlaced_StartsTheGame()
    {
        var first = new Player("Olivier", isBot: false, new Board(BoardSize.Standard));
        var second = new Player("Ulysse", isBot: false, new Board(BoardSize.Standard));
        var game = new Game(GameMode.Local, first, second);

        game.PlaceFleetFromClient(ValidFleet());
        game.PlaceFleetFromClient(ValidFleet());

        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Equal("Olivier", game.ViewForClient().ViewerName);
        Assert.Empty(game.ViewForClient().FleetToPlace);
    }

    /// <summary>
    /// Remarque de la revue de la PR #5. Le verrou de l'agrégat doit sérialiser
    /// les soumissions comme il sérialise les tirs : une seule flotte acceptée,
    /// et jamais de grille à moitié remplie par deux soumissions entrelacées.
    /// </summary>
    [Fact]
    public void PlaceFleetFromClient_CalledConcurrently_AcceptsExactlyOneFleet()
    {
        var game = AwaitingFleetGame();
        var accepted = 0;

        // Les fils partent sur un signal commun, et le signal attend qu'ils
        // soient tous gares dessus. Sans cette attente, les premiers fils
        // terminent pendant que les derniers sont encore crees : la course ne se
        // produit jamais et le test passe meme sans verrou — verifie.
        using var start = new ManualResetEventSlim(false);

        var threads = Enumerable.Range(0, 64).Select(_ => new Thread(() =>
        {
            start.Wait();

            if (game.PlaceFleetFromClient(ValidFleet()).IsAccepted)
            {
                Interlocked.Increment(ref accepted);
            }
        })).ToList();

        threads.ForEach(thread => thread.Start());
        Thread.Sleep(20);
        start.Set();
        threads.ForEach(thread => thread.Join());

        Assert.Equal(1, accepted);
        Assert.Equal(GameStatus.InProgress, game.Status);

        // Sans verrou, la grille recoit jusqu'a 18 navires : deux soumissions
        // entrelacees posent chacune les leurs.
        Assert.Equal(5, game.ViewForClient().OwnFleet.Count);
    }
}
