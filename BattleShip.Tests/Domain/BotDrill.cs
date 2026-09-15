using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

/// <summary>
/// Fait tirer une stratégie sur une grille jusqu'à ce que la flotte coule, sans
/// passer par <see cref="Game"/> : la mesure porte sur la stratégie seule, pas
/// sur l'alternance des tours ni sur la course entre les deux joueurs.
/// </summary>
internal static class BotDrill
{
    public static GameView ViewOf(BoardSize size, IReadOnlyList<RevealedCell> fired) =>
        new(Guid.Empty, GameStatus.InProgress, GameMode.Solo, size, "Bot", "Humain", true, [], [], fired, BotDifficulty.Random, [],
            [.. FleetTemplate.Standard.Select(kind => new ShipToPlace(kind, kind.Size()))], null);

    public static int ShotsToClear(IBotStrategy strategy, Board target, BoardSize size)
    {
        var fired = new List<RevealedCell>();
        var played = new HashSet<Coordinates>();

        while (!target.AllShipsSunk)
        {
            var cell = strategy.ChooseTarget(ViewOf(size, fired), size);

            Assert.True(size.Contains(cell), $"case {cell} hors grille");
            Assert.True(played.Add(cell), $"la stratégie a rejoué la case {cell}");

            fired.Add(new RevealedCell(cell, target.Receive(cell)));
        }

        return fired.Count;
    }

    public static double AverageShotsToClear(Func<int, IBotStrategy> strategyForSeed, int games = 100)
    {
        var total = 0;

        for (var seed = 1; seed <= games; seed++)
        {
            var board = new RandomFleetPlacer(new Random(seed)).Place(BoardSize.Standard, FleetTemplate.Standard);
            total += ShotsToClear(strategyForSeed(seed), board, BoardSize.Standard);
        }

        return (double)total / games;
    }
}
