using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class HuntTargetParityBotTests
{
    private const int Seeds = 100;

    private static bool IsOnTheScanLattice(Coordinates cell) => (cell.Column + cell.Row) % 2 is 0;

    private static List<Coordinates> ChoicesOverManySeeds(BoardSize size, params RevealedCell[] fired) =>
        [.. Enumerable.Range(1, Seeds)
            .Select(seed => new HuntTargetParityBot(new Random(seed)).ChooseTarget(BotDrill.ViewOf(size, fired), size))];

    /// <summary>
    /// Le plus petit navire de la flotte occupe deux cases adjacentes, donc au
    /// moins une case du damier : n'en balayer que la moitié ne peut pas manquer
    /// un navire. Cette garantie tombe le jour où la flotte devient
    /// personnalisable avec un navire d'une seule case (backlog item 6).
    /// </summary>
    [Fact]
    public void ChooseTarget_WhileHunting_OnlyScansOneCellOutOfTwo()
    {
        Assert.All(
            ChoicesOverManySeeds(BoardSize.Standard),
            cell => Assert.True(IsOnTheScanLattice(cell), $"la case {cell} est hors du damier de balayage"));
    }

    /// <summary>
    /// Le filtre ne vaut que pour la chasse : les voisins d'une case touchée sont
    /// tous de la parité opposée. Un filtre appliqué à la traque empêcherait le
    /// bot d'achever le navire qu'il vient de toucher.
    /// </summary>
    [Fact]
    public void ChooseTarget_WhileTracking_IgnoresTheScanLattice()
    {
        RevealedCell[] fired = [new(new Coordinates(3, 3), ShotResult.Hit)];

        Assert.All(
            ChoicesOverManySeeds(BoardSize.Standard, fired),
            cell =>
            {
                Assert.False(IsOnTheScanLattice(cell), $"la case {cell} ne peut pas toucher (3,3)");
                Assert.Equal(1, Math.Abs(cell.Column - 3) + Math.Abs(cell.Row - 3));
            });
    }

    [Fact]
    public void ChooseTarget_WhenTheWholeLatticeIsFired_FallsBackToTheRemainingCells()
    {
        var size = new BoardSize(4, 4);

        RevealedCell[] fired =
        [
            .. Enumerable.Range(0, size.Columns)
                .SelectMany(column => Enumerable.Range(0, size.Rows).Select(row => new Coordinates(column, row)))
                .Where(IsOnTheScanLattice)
                .Select(cell => new RevealedCell(cell, ShotResult.Miss))
        ];

        Assert.All(
            ChoicesOverManySeeds(size, fired),
            cell => Assert.False(IsOnTheScanLattice(cell), $"la case {cell} a déjà été tirée"));
    }
}
