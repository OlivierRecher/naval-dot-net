using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class FleetTemplateRulesTests
{
    private static readonly BoardSize Size = BoardSize.Standard;

    [Fact]
    public void Validate_OnTheStandardFleet_Accepts() =>
        Assert.Null(FleetTemplateRules.Validate(Size, FleetTemplate.Standard));

    [Fact]
    public void Validate_OnAnEmptyFleet_Refuses() =>
        Assert.Equal(FleetTemplateError.Empty, FleetTemplateRules.Validate(Size, []));

    [Fact]
    public void Validate_OnAShipLongerThanTheBoard_Refuses() =>
        Assert.Equal(
            FleetTemplateError.ShipLongerThanTheBoard,
            FleetTemplateRules.Validate(new BoardSize(4, 4), [ShipKind.Carrier]));

    [Fact]
    public void Validate_OnTooManyShips_Refuses() =>
        Assert.Equal(
            FleetTemplateError.TooManyShips,
            FleetTemplateRules.Validate(Size, [.. Enumerable.Repeat(ShipKind.PatrolBoat, 16)]));

    [Fact]
    public void Validate_OnAFleetThatFillsTheBoard_Refuses() =>
        Assert.Equal(
            FleetTemplateError.TooDense,
            FleetTemplateRules.Validate(new BoardSize(8, 8), [.. Enumerable.Repeat(ShipKind.Carrier, 5)]));

    /// <summary>
    /// Le plafond de densité n'est pas choisi mais mesuré : à la densité
    /// maximale acceptée, sur la plus petite grille autorisée, le placement
    /// aléatoire doit réussir <b>à chaque fois</b>. S'il échouait ne serait-ce
    /// qu'une fois sur 200 graines, le refus dépendrait du hasard et non de la
    /// composition — c'est exactement ce que ce plafond doit empêcher.
    /// </summary>
    [Fact]
    public void AtTheDensityCeiling_RandomPlacementNeverFails()
    {
        var size = new BoardSize(8, 8);
        var budget = (int)(size.Columns * size.Rows * FleetTemplateRules.MaxOccupancy);

        var fleet = new List<ShipKind>();
        while (fleet.Sum(kind => kind.Size()) + 5 <= budget && fleet.Count < FleetTemplateRules.MaxShips)
        {
            fleet.Add(ShipKind.Carrier);
        }

        Assert.Null(FleetTemplateRules.Validate(size, fleet));
        Assert.True(fleet.Count >= 4, $"flotte trop petite pour éprouver le plafond : {fleet.Count} navires");

        for (var seed = 1; seed <= 200; seed++)
        {
            var board = new RandomFleetPlacer(new Random(seed)).Place(size, fleet);
            Assert.Equal(fleet.Count, board.Ships.Count);
        }
    }
}
