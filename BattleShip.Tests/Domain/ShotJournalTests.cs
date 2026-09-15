using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

/// <summary>
/// Contrôles annoncés par l'ADR 0002 : le journal de tirs doit suffire à
/// reconstituer le déroulé d'une partie, sans quoi l'historique, le rejeu et
/// les statistiques du backlog item 5 reposeraient sur du vide.
/// </summary>
public class ShotJournalTests
{
    private static Game PlayToTheEnd(int seed)
    {
        var placer = new RandomFleetPlacer(new Random(seed));

        var alice = new Player("Alice", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var bob = new Player("Bob", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        var game = new Game(GameMode.Local, alice, bob);
        var strategy = new RandomBot(new Random(seed + 1));

        while (game.Status is GameStatus.InProgress)
        {
            var target = strategy.ChooseTarget(game.ViewFor(game.CurrentPlayer), BoardSize.Standard);
            Assert.True(game.Fire(target).IsAccepted, $"tir refusé en {target}");
        }

        return game;
    }

    private static Board Rebuild(Board original)
    {
        var board = new Board(original.Size);

        foreach (var ship in original.Ships)
        {
            Assert.Null(board.Place(ship.Placement));
        }

        return board;
    }

    [Theory]
    [InlineData(3)]
    [InlineData(19)]
    [InlineData(57)]
    public void Shots_ReplayedOnFreshBoards_ReproduceEveryResultAndTheSameWinner(int seed)
    {
        var game = PlayToTheEnd(seed);
        Player[] players = [game.CurrentPlayer, game.Opponent];

        var boards = players.ToDictionary(player => player.Id, player => Rebuild(player.Board));

        Guid? winnerFromJournal = null;

        foreach (var shot in game.Shots)
        {
            var targetId = players.Single(player => player.Id != shot.ShooterId).Id;

            // Le journal doit décrire fidèlement chaque tir, pas seulement sa cible.
            Assert.Equal(shot.Result, boards[targetId].Receive(shot.Target));

            if (boards[targetId].AllShipsSunk)
            {
                winnerFromJournal = shot.ShooterId;
                break;
            }
        }

        Assert.Equal(game.Winner!.Id, winnerFromJournal);
    }

    [Fact]
    public void Shots_ContainsExactlyOneEntryPerAcceptedShot()
    {
        var game = PlayToTheEnd(11);

        Assert.Equal(game.Shots.Count, game.Shots.Distinct().Count());
        Assert.Equal(ShotResult.Sunk, game.Shots[^1].Result);
        Assert.Equal(game.Winner!.Id, game.Shots[^1].ShooterId);
    }

    [Fact]
    public void Shots_CountsPerPlayer_NeverDifferByMoreThanOne()
    {
        // Consequence directe de l'alternance : si le tour sautait un joueur,
        // l'ecart se creuserait.
        var game = PlayToTheEnd(23);

        var counts = game.Shots.GroupBy(shot => shot.ShooterId).Select(group => group.Count()).ToList();

        Assert.Equal(2, counts.Count);
        Assert.True(Math.Abs(counts[0] - counts[1]) <= 1, $"écart de {Math.Abs(counts[0] - counts[1])} tirs");
    }
}
