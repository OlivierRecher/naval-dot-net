using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

/// <summary>
/// Le repository est enregistre en Singleton : deux requetes visant la meme
/// partie — y compris une HTTP et une gRPC — atteignent le meme agregat en
/// parallele. Ces tests protegent la transition et la projection, pas la table
/// de stockage que le ConcurrentDictionary couvre deja. Voir ADR 0004.
/// </summary>
public class GameConcurrencyTests
{
    private static Game LocalDuel(int side)
    {
        var size = new BoardSize(side, side);
        var placer = new RandomFleetPlacer(new Random(31));
        var alice = new Player("Alice", isBot: false, placer.Place(size, FleetTemplate.Standard));
        var bob = new Player("Bob", isBot: false, placer.Place(size, FleetTemplate.Standard));

        return new Game(GameMode.Local, alice, bob);
    }

    [Fact]
    public void FireFromClient_CalledConcurrently_RecordsExactlyOneShotPerAcceptedCall()
    {
        var game = LocalDuel(20);
        var targets = Enumerable.Range(0, 400).Select(index => new Coordinates(index % 20, index / 20));
        var accepted = 0;

        Parallel.ForEach(targets, target =>
        {
            if (game.FireFromClient(target).IsAccepted)
            {
                Interlocked.Increment(ref accepted);
            }
        });

        Assert.Equal(accepted, game.Shots.Count);
    }

    [Fact]
    public async Task ViewFor_WhileAnotherThreadFires_NeverObservesAJournalBeingMutated()
    {
        // Sans synchronisation, l'enumeration du journal pendant un ajout leve
        // « Collection was modified ». Le test ne peut pas prouver l'absence de
        // course, il en attrape l'occurrence : il echoue vite, il ne passe
        // jamais a tort.
        var game = LocalDuel(20);
        var viewer = game.CurrentPlayer;
        Exception? observed = null;

        var firing = Task.Run(() =>
        {
            for (var column = 0; column < 20; column++)
            {
                for (var row = 0; row < 20; row++)
                {
                    game.FireFromClient(new Coordinates(column, row));
                }
            }
        });

        var projecting = Task.Run(() =>
        {
            try
            {
                while (!firing.IsCompleted)
                {
                    _ = game.ViewFor(viewer);
                    _ = game.Shots.Count;
                }
            }
            catch (Exception error)
            {
                observed = error;
            }
        });

        await Task.WhenAll(firing, projecting);

        Assert.Null(observed);
    }
}
