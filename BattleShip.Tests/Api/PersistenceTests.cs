using BattleShip.API.Persistence;
using BattleShip.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BattleShip.Tests.Api;

/// <summary>
/// Un vrai SQLite, en mémoire. Chaque « redémarrage » est un nouveau dépôt et un
/// nouveau cache sur la même base : c'est le seul moyen de vérifier qu'une
/// partie tient dans ce qui est écrit, et non dans ce qui reste en mémoire.
/// </summary>
public class PersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public PersistenceTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private BattleShipDbContext NewContext() =>
        new(new DbContextOptionsBuilder<BattleShipDbContext>().UseSqlite(_connection).Options);

    /// <summary>Un dépôt neuf sur un cache neuf : l'équivalent d'un redémarrage.</summary>
    private SqliteGameRepository AfterRestart() => new(NewContext(), new GameCache());

    private static Game NewGame(FleetPlacement placement = FleetPlacement.Random)
    {
        var placer = new RandomFleetPlacer(new Random(31));

        var human = new Player("Olivier", isBot: false, placement is FleetPlacement.Manual
            ? new Board(BoardSize.Standard)
            : placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        var bot = new Player("Bot", isBot: true, placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        return new Game(GameMode.Solo, human, bot, BotDifficulty.HuntTarget);
    }

    private static List<ShipPlacement> ValidFleet() =>
    [
        new(ShipKind.Carrier, new Coordinates(0, 0), Orientation.Vertical),
        new(ShipKind.Battleship, new Coordinates(2, 0), Orientation.Vertical),
        new(ShipKind.Cruiser, new Coordinates(4, 0), Orientation.Vertical),
        new(ShipKind.Submarine, new Coordinates(6, 0), Orientation.Vertical),
        new(ShipKind.Destroyer, new Coordinates(8, 0), Orientation.Vertical)
    ];

    [Fact]
    public void AGameInProgress_SurvivesARestart()
    {
        var game = NewGame();
        var repository = AfterRestart();
        repository.Add(game);

        var strategy = new BotStrategyFactory(new Random(31)).For(BotDifficulty.HuntTarget);
        for (var turn = 0; turn < 8; turn++)
        {
            game.FireFromClient(new Coordinates(turn, 0));
            game.PlayBotTurn(strategy);
        }

        repository.Save(game);

        var reloaded = AfterRestart().Find(game.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(game.Id, reloaded.Id);
        Assert.Equal(game.Status, reloaded.Status);
        Assert.Equal(game.BotDifficulty, reloaded.BotDifficulty);
        Assert.Equal([.. game.Shots], [.. reloaded.Shots]);
        Assert.Equal(game.ViewForClient().ViewerName, reloaded.ViewForClient().ViewerName);
        Assert.Equal(
            [.. game.ViewForClient().ShotsReceived],
            [.. reloaded.ViewForClient().ShotsReceived]);
    }

    /// <summary>
    /// Le test qui donne son sens à <c>Save</c>. Sans lui, l'agrégat change en
    /// mémoire et la base ne le sait pas : au redémarrage, les tirs ont disparu.
    /// Un dépôt en mémoire ne pouvait pas faire apparaître ce besoin.
    /// </summary>
    [Fact]
    public void AGame_MutatedWithoutSave_LosesItsShotsAcrossARestart()
    {
        var game = NewGame();
        var repository = AfterRestart();
        repository.Add(game);

        game.FireFromClient(new Coordinates(3, 3));

        var reloaded = AfterRestart().Find(game.Id);

        Assert.NotNull(reloaded);
        Assert.Single(game.Shots);
        Assert.Empty(reloaded.Shots);
    }

    [Fact]
    public void AFinishedGame_KeepsItsWinnerAcrossARestart()
    {
        var game = NewGame();
        var repository = AfterRestart();
        repository.Add(game);

        var strategy = new BotStrategyFactory(new Random(31)).For(BotDifficulty.HuntTargetParity);
        foreach (var cell in Enumerable.Range(0, 100).Select(i => new Coordinates(i % 10, i / 10)))
        {
            if (game.Status is GameStatus.Finished) break;
            game.FireFromClient(cell);
            game.PlayBotTurn(strategy);
        }

        repository.Save(game);

        Assert.Equal(GameStatus.Finished, game.Status);

        var reloaded = AfterRestart().Find(game.Id);

        Assert.Equal(GameStatus.Finished, reloaded!.Status);
        Assert.Equal(game.Winner!.Name, reloaded.Winner!.Name);
    }

    /// <summary>
    /// En placement manuel, la flotte n'existe pas à la création : elle arrive
    /// plus tard et doit être écrite à ce moment-là, sinon le rejeu repose sur
    /// une grille vide.
    /// </summary>
    [Fact]
    public void AManuallyPlacedFleet_IsWrittenWhenItArrives()
    {
        var game = NewGame(FleetPlacement.Manual);
        var repository = AfterRestart();
        repository.Add(game);

        Assert.Equal(GameStatus.AwaitingFleet, AfterRestart().Find(game.Id)!.Status);

        game.PlaceFleetFromClient(ValidFleet());
        repository.Save(game);

        var reloaded = AfterRestart().Find(game.Id);

        Assert.Equal(GameStatus.InProgress, reloaded!.Status);
        Assert.Equal(5, reloaded.ViewForClient().OwnFleet.Count);
    }

    /// <summary>
    /// Le résultat de chaque tir est stocké alors qu'il est <b>dérivable</b> : il
    /// sert aux statistiques en SQL, jamais au rejeu. Ce test interdit qu'il
    /// diverge de ce que le rejeu recalcule.
    /// </summary>
    [Fact]
    public void TheStoredShotResults_NeverDivergeFromTheReplayedOnes()
    {
        var game = NewGame();
        var repository = AfterRestart();
        repository.Add(game);

        var strategy = new BotStrategyFactory(new Random(31)).For(BotDifficulty.HuntTarget);
        for (var turn = 0; turn < 25 && game.Status is GameStatus.InProgress; turn++)
        {
            game.FireFromClient(new Coordinates(turn % 10, turn / 10));
            game.PlayBotTurn(strategy);
        }

        repository.Save(game);

        using var db = NewContext();
        var stored = db.Shots
            .Where(shot => shot.GameId == game.Id)
            .OrderBy(shot => shot.Ordinal)
            .Select(shot => shot.Result)
            .ToList();

        var replayed = AfterRestart().Find(game.Id)!.Shots.Select(shot => shot.Result.ToString()).ToList();

        Assert.NotEmpty(stored);
        Assert.Equal(replayed, stored);
    }

    [Fact]
    public void Find_OnAnUnknownGame_ReturnsNull() =>
        Assert.Null(AfterRestart().Find(Guid.NewGuid()));

    /// <summary>
    /// Le cache n'est pas une optimisation : sans lui, deux lectures rendraient
    /// deux instances, et le verrou par partie de l'ADR 0008 ne sérialiserait
    /// plus rien.
    /// </summary>
    [Fact]
    public void Find_TwiceInTheSameProcess_ReturnsTheSameInstance()
    {
        var game = NewGame();
        var cache = new GameCache();
        var repository = new SqliteGameRepository(NewContext(), cache);
        repository.Add(game);

        var first = new SqliteGameRepository(NewContext(), cache).Find(game.Id);
        var second = new SqliteGameRepository(NewContext(), cache).Find(game.Id);

        Assert.Same(first, second);
        Assert.Same(game, first);
    }

    /// <summary>
    /// Les endpoints valident après <b>chaque</b> mutation, jamais une seule fois
    /// en fin de partie. Le journal est en ajout seul : une validation doit
    /// écrire les tirs manquants, pas réécrire ceux qui sont déjà là.
    /// </summary>
    [Fact]
    public void SavingAfterEveryShot_AppendsInsteadOfRewriting()
    {
        var game = NewGame();
        var repository = AfterRestart();
        repository.Add(game);

        var strategy = new BotStrategyFactory(new Random(31)).For(BotDifficulty.HuntTarget);

        for (var turn = 0; turn < 10 && game.Status is GameStatus.InProgress; turn++)
        {
            game.FireFromClient(new Coordinates(turn, 0));
            repository.Save(game);
            game.PlayBotTurn(strategy);
            repository.Save(game);
        }

        using var db = NewContext();
        var ordinals = db.Shots.Where(shot => shot.GameId == game.Id).Select(shot => shot.Ordinal).ToList();

        Assert.Equal(game.Shots.Count, ordinals.Count);
        Assert.Equal([.. Enumerable.Range(0, game.Shots.Count)], [.. ordinals.Order()]);
        Assert.Equal([.. game.Shots], [.. AfterRestart().Find(game.Id)!.Shots]);
    }

    /// <summary>
    /// L'identité de la partie tient à <c>Remember</c>, pas au raccourci de
    /// <c>Find</c> : même en forçant une lecture SQL, c'est l'instance déjà
    /// publiée qui est rendue.
    /// </summary>
    [Fact]
    public void Load_OfAnAlreadyPublishedGame_StillReturnsTheSameInstance()
    {
        var game = NewGame();
        var cache = new GameCache();
        new SqliteGameRepository(NewContext(), cache).Add(game);

        var rebuilt = new SqliteGameRepository(NewContext(), new GameCache()).Find(game.Id);
        var published = new SqliteGameRepository(NewContext(), cache).Find(game.Id);

        Assert.NotSame(game, rebuilt);
        Assert.Same(game, published);
    }

    /// <summary>
    /// Le seul scénario qui distingue une publication atomique d'un simple
    /// écrasement : deux requêtes arrivent sur un cache froid, toutes deux
    /// reconstruisent la partie, et une seule instance doit survivre. Sinon,
    /// chacune joue sur la sienne — et le verrou de l'agrégat (ADR 0008) ne
    /// sérialise plus rien du tout.
    ///
    /// Base sur fichier, pas en mémoire : chaque fil doit ouvrir sa propre
    /// connexion, et une connexion SQLite n'est pas partageable entre fils.
    /// </summary>
    [Fact]
    public void Find_ConcurrentlyOnAColdCache_PublishesASingleInstance()
    {
        var file = Path.Combine(Path.GetTempPath(), $"battleship-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<BattleShipDbContext>()
                .UseSqlite($"Data Source={file}")
                .Options;

            using (var setup = new BattleShipDbContext(options))
            {
                setup.Database.EnsureCreated();
            }

            var game = NewGame();
            using (var context = new BattleShipDbContext(options))
            {
                new SqliteGameRepository(context, new GameCache()).Add(game);
            }

            var cache = new GameCache();
            var found = new Game?[32];
            using var start = new ManualResetEventSlim(false);

            var threads = Enumerable.Range(0, found.Length).Select(index => new Thread(() =>
            {
                using var context = new BattleShipDbContext(options);
                var repository = new SqliteGameRepository(context, cache);

                start.Wait();

                found[index] = repository.Find(game.Id);
            })).ToList();

            threads.ForEach(thread => thread.Start());
            Thread.Sleep(20);
            start.Set();
            threads.ForEach(thread => thread.Join());

            Assert.All(found, instance => Assert.NotNull(instance));
            Assert.Single(found.Distinct(ReferenceEqualityComparer.Instance));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }
}
