using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

/// <summary>
/// L'ADR 0009 annonçait la limite que ces tests soldent : le damier d'une case
/// sur deux ne peut rien manquer <b>tant que</b> le plus petit navire en occupe
/// deux. Avec une flotte personnalisable, ce n'est plus acquis — le pas du
/// balayage doit se déduire de la flotte réellement en jeu.
/// </summary>
public class ScanLatticeTests
{
    private static readonly BoardSize Size = BoardSize.Standard;

    private static GameView ViewWith(IReadOnlyList<ShipKind> fleet) => new(
        Guid.Empty, GameStatus.InProgress, GameMode.Solo, Size, "Bot", "Humain", true,
        [], [], [], BotDifficulty.HuntTargetParity, [],
        [.. fleet.Select(kind => new ShipToPlace(kind, kind.Size()))], null);

    private static List<Coordinates> ChoicesOverManySeeds(IReadOnlyList<ShipKind> fleet) =>
        [.. Enumerable.Range(1, 100)
            .Select(seed => new HuntTargetParityBot(new Random(seed)).ChooseTarget(ViewWith(fleet), Size))];

    [Fact]
    public void WithTheStandardFleet_TheLatticeIsOneCellOutOfTwo()
    {
        var chosen = ChoicesOverManySeeds(FleetTemplate.Standard);

        Assert.All(chosen, cell => Assert.Equal(0, (cell.Column + cell.Row) % 2));
        Assert.True(chosen.Distinct().Count() > 10, "le balayage est bloqué sur quelques cases");
    }

    /// <summary>
    /// Le cas que l'ADR 0009 annonçait comme rendant le niveau <b>incorrect</b>.
    /// Avec un navire d'une seule case, aucune maille ne peut être sautée.
    /// </summary>
    [Fact]
    public void WithAOneCellShip_TheLatticeCoversEveryCell()
    {
        var chosen = ChoicesOverManySeeds([ShipKind.Carrier, ShipKind.PatrolBoat]);

        Assert.Contains(chosen, cell => (cell.Column + cell.Row) % 2 is 1);
    }

    /// <summary>
    /// Généralisation : un navire de longueur L croise forcément une maille de
    /// pas L, jamais moins. Le pas suit donc le plus petit navire de la flotte.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void WithOnlyLargeShips_TheLatticeWidensToTheSmallestOne(int smallest)
    {
        IReadOnlyList<ShipKind> fleet = smallest switch
        {
            3 => [ShipKind.Carrier, ShipKind.Cruiser],
            4 => [ShipKind.Carrier, ShipKind.Battleship],
            _ => [ShipKind.Carrier]
        };

        Assert.All(
            ChoicesOverManySeeds(fleet),
            cell => Assert.Equal(0, (cell.Column + cell.Row) % smallest));
    }

    /// <summary>
    /// La maille ne vaut que pour la chasse. Une case touchée impose de viser ses
    /// voisines, qui ne sont jamais sur la même maille.
    /// </summary>
    [Fact]
    public void WhileTracking_TheLatticeIsIgnoredWhateverTheFleet()
    {
        var view = new GameView(
            Guid.Empty, GameStatus.InProgress, GameMode.Solo, Size, "Bot", "Humain", true,
            [], [], [new RevealedCell(new Coordinates(3, 3), ShotResult.Hit)],
            BotDifficulty.HuntTargetParity, [],
            [new ShipToPlace(ShipKind.Carrier, 5)], null);

        Assert.All(
            Enumerable.Range(1, 50).Select(seed => new HuntTargetParityBot(new Random(seed)).ChooseTarget(view, Size)),
            cell => Assert.Equal(1, Math.Abs(cell.Column - 3) + Math.Abs(cell.Row - 3)));
    }

    /// <summary>
    /// Sans information de flotte — une vue construite par un appelant qui ne la
    /// renseigne pas — le bot ne doit sauter aucune case : mieux vaut un
    /// balayage complet qu'un balayage qui manque un navire.
    /// </summary>
    [Fact]
    public void WithoutAnyFleetInformation_TheLatticeCoversEveryCell()
    {
        var chosen = ChoicesOverManySeeds([]);

        Assert.Contains(chosen, cell => (cell.Column + cell.Row) % 2 is 1);
    }
}
