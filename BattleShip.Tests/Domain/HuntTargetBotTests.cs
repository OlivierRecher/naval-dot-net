using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class HuntTargetBotTests
{
    private const int Seeds = 100;

    private static readonly BoardSize Size = BoardSize.Standard;

    /// <summary>
    /// Une seule graine ne distingue pas « le bot traque » de « le bot a eu de la
    /// chance » : c'est la distribution sur un grand nombre de graines qui porte
    /// l'information.
    /// </summary>
    private static List<Coordinates> ChoicesOverManySeeds(params RevealedCell[] fired) =>
        [.. Enumerable.Range(1, Seeds)
            .Select(seed => new HuntTargetBot(new Random(seed)).ChooseTarget(BotDrill.ViewOf(Size, fired), Size))];

    private static bool IsNextTo(Coordinates cell, IEnumerable<RevealedCell> fired) =>
        fired.Any(shot => Math.Abs(shot.Target.Column - cell.Column) + Math.Abs(shot.Target.Row - cell.Row) is 1);

    [Fact]
    public void ChooseTarget_WhileAShipIsOnlyDamaged_NeverLeavesItsSurroundings()
    {
        RevealedCell[] fired = [new(new Coordinates(3, 3), ShotResult.Hit)];

        Assert.All(
            ChoicesOverManySeeds(fired),
            cell => Assert.True(IsNextTo(cell, fired), $"la case {cell} ne touche pas le navire endommagé"));
    }

    /// <summary>
    /// Pendant réciproque du test précédent : une fois le navire coulé, le bot
    /// doit redevenir capable de tirer loin. Les deux ensemble décrivent la
    /// bascule traque → chasse, qu'aucun des deux ne prouve seul.
    /// </summary>
    [Fact]
    public void ChooseTarget_OnceTheShipIsSunk_GoesBackToHunting()
    {
        RevealedCell[] fired =
        [
            new(new Coordinates(3, 3), ShotResult.Hit),
            new(new Coordinates(4, 3), ShotResult.Sunk)
        ];

        Assert.Contains(ChoicesOverManySeeds(fired), cell => !IsNextTo(cell, fired));
    }

    /// <summary>
    /// Le contact étant autorisé, un navire coulé peut être mitoyen d'un navire
    /// encore vivant : les cases touchées de celui-ci restent des amorces.
    /// </summary>
    [Fact]
    public void ChooseTarget_WhenADamagedShipRemainsElsewhere_KeepsTrackingIt()
    {
        RevealedCell[] fired =
        [
            new(new Coordinates(3, 3), ShotResult.Hit),
            new(new Coordinates(4, 3), ShotResult.Sunk),
            new(new Coordinates(8, 8), ShotResult.Hit)
        ];

        Assert.All(
            ChoicesOverManySeeds(fired),
            cell => Assert.True(
                IsNextTo(cell, [new RevealedCell(new Coordinates(8, 8), ShotResult.Hit)]),
                $"la case {cell} abandonne le navire encore endommagé en (8,8)"));
    }

    [Fact]
    public void ChooseTarget_WithTwoAlignedHits_ExtendsTheLineInsteadOfProbingSideways()
    {
        RevealedCell[] fired =
        [
            new(new Coordinates(3, 3), ShotResult.Hit),
            new(new Coordinates(4, 3), ShotResult.Hit)
        ];

        Coordinates[] alongTheLine = [new(2, 3), new(5, 3)];

        Assert.All(
            ChoicesOverManySeeds(fired),
            cell => Assert.Contains(cell, alongTheLine));
    }

    [Fact]
    public void ChooseTarget_OnAnUntouchedBoard_HuntsAnywhere()
    {
        var chosen = ChoicesOverManySeeds().Distinct().ToList();

        Assert.True(chosen.Count > 4, $"seulement {chosen.Count} cases distinctes : le balayage est bloqué");
        Assert.All(chosen, cell => Assert.True(Size.Contains(cell), $"case {cell} hors grille"));
    }

    [Fact]
    public void ChooseTarget_OverAWholeGame_NeverRepeatsACell()
    {
        var board = new RandomFleetPlacer(new Random(7)).Place(Size, FleetTemplate.Standard);

        var shots = BotDrill.ShotsToClear(new HuntTargetBot(new Random(7)), board, Size);

        Assert.InRange(shots, FleetTemplate.Standard.Sum(kind => kind.Size()), Size.Columns * Size.Rows);
    }

    [Fact]
    public void ChooseTarget_OnAFullyFiredBoard_Throws()
    {
        var size = new BoardSize(2, 1);
        RevealedCell[] fired =
        [
            new(new Coordinates(0, 0), ShotResult.Miss),
            new(new Coordinates(1, 0), ShotResult.Miss)
        ];

        Assert.Throws<InvalidOperationException>(
            () => new HuntTargetBot(new Random(1)).ChooseTarget(BotDrill.ViewOf(size, fired), size));
    }
}
