using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

/// <summary>
/// L'ADR 0002 annonce que le journal en ajout seul fournit « rejeu, historique et
/// statistiques presque gratuitement ». Ces tests l'établissent : une partie se
/// reconstruit à partir des seuls placements et du journal ordonné, sans
/// persister aucun état dérivé.
/// </summary>
public class GameRestoreTests
{
    private static (Game Game, IReadOnlyList<ShipPlacement> First, IReadOnlyList<ShipPlacement> Second) Played(int turns)
    {
        var placer = new RandomFleetPlacer(new Random(21));
        var firstBoard = placer.Place(BoardSize.Standard, FleetTemplate.Standard);
        var secondBoard = placer.Place(BoardSize.Standard, FleetTemplate.Standard);

        var first = new Player("Olivier", isBot: false, firstBoard);
        var second = new Player("Bot", isBot: true, secondBoard);
        var game = new Game(GameMode.Solo, first, second, BotDifficulty.HuntTarget);
        var strategy = new BotStrategyFactory(new Random(21)).For(BotDifficulty.HuntTarget);

        for (var turn = 0; turn < turns && game.Status is GameStatus.InProgress; turn++)
        {
            game.FireFromClient(new Coordinates(turn % 10, turn / 10));
            game.PlayBotTurn(strategy);
        }

        return (game,
            [.. firstBoard.Ships.Select(ship => ship.Placement)],
            [.. secondBoard.Ships.Select(ship => ship.Placement)]);
    }

    private static Game Rebuild(Game original, IReadOnlyList<ShipPlacement> first, IReadOnlyList<ShipPlacement> second)
    {
        var firstBoard = new Board(BoardSize.Standard);
        foreach (var placement in first) firstBoard.Place(placement);

        var secondBoard = new Board(BoardSize.Standard);
        foreach (var placement in second) secondBoard.Place(placement);

        return Game.Restore(
            original.Id,
            original.Mode,
            new Player("Olivier", isBot: false, firstBoard, original.Shots[0].ShooterId),
            new Player("Bot", isBot: true, secondBoard, original.Shots.First(s => s.ShooterId != original.Shots[0].ShooterId).ShooterId),
            original.BotDifficulty,
            [.. original.Shots.Select(shot => shot.Target)]);
    }

    [Fact]
    public void Restore_FromPlacementsAndJournal_RebuildsTheSameGame()
    {
        var (original, first, second) = Played(12);

        var rebuilt = Rebuild(original, first, second);

        Assert.Equal(original.Id, rebuilt.Id);
        Assert.Equal(original.Mode, rebuilt.Mode);
        Assert.Equal(original.BotDifficulty, rebuilt.BotDifficulty);
        Assert.Equal(original.Status, rebuilt.Status);
        Assert.Equal(original.CurrentPlayer.Name, rebuilt.CurrentPlayer.Name);
        Assert.Equal(original.Shots.Count, rebuilt.Shots.Count);
        Assert.Equal([.. original.Shots], [.. rebuilt.Shots]);
    }

    /// <summary>
    /// L'état dérivé — cases touchées, navires coulés, tirs reçus — n'est pas
    /// persisté : s'il ne se reconstruit pas exactement, le rejeu est faux.
    /// </summary>
    [Fact]
    public void Restore_RebuildsEveryDerivedState()
    {
        var (original, first, second) = Played(20);

        var rebuilt = Rebuild(original, first, second);

        var before = original.ViewForClient();
        var after = rebuilt.ViewForClient();

        Assert.Equal(before.ViewerName, after.ViewerName);
        Assert.Equal(before.IsViewerTurn, after.IsViewerTurn);
        Assert.Equal([.. before.ShotsReceived], [.. after.ShotsReceived]);
        Assert.Equal([.. before.ShotsFired], [.. after.ShotsFired]);
        Assert.Equal(
            [.. before.OwnFleet.Select(ship => (ship.Kind, ship.IsSunk, ship.Hits.Count))],
            [.. after.OwnFleet.Select(ship => (ship.Kind, ship.IsSunk, ship.Hits.Count))]);
    }

    [Fact]
    public void Restore_OfAFinishedGame_KeepsTheWinner()
    {
        var (original, first, second) = Played(200);

        Assert.Equal(GameStatus.Finished, original.Status);

        var rebuilt = Rebuild(original, first, second);

        Assert.Equal(GameStatus.Finished, rebuilt.Status);
        Assert.Equal(original.Winner!.Name, rebuilt.Winner!.Name);
    }

    [Fact]
    public void Restore_WithoutAnyShot_IsANewGame()
    {
        var placer = new RandomFleetPlacer(new Random(2));
        var first = new Player("Olivier", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var second = new Player("Bot", isBot: true, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var id = Guid.NewGuid();

        var game = Game.Restore(id, GameMode.Solo, first, second, BotDifficulty.Random, []);

        Assert.Equal(id, game.Id);
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Empty(game.Shots);
    }

    [Fact]
    public void Restore_OfAGameAwaitingItsFleet_StaysAwaiting()
    {
        var human = new Player("Olivier", isBot: false, new Board(BoardSize.Standard));
        var bot = new Player("Bot", isBot: true,
            new RandomFleetPlacer(new Random(2)).Place(BoardSize.Standard, FleetTemplate.Standard));

        var game = Game.Restore(Guid.NewGuid(), GameMode.Solo, human, bot, BotDifficulty.Random, []);

        Assert.Equal(GameStatus.AwaitingFleet, game.Status);
    }
}
