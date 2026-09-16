using System.Net;
using System.Net.Http.Json;
using BattleShip.Models;

namespace BattleShip.Tests.Api;

public class CustomFleetEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateGameRequest Request(
        IReadOnlyList<string>? fleet, int side = 10, string placement = "Random") =>
        new("Olivier", side, side, "Solo", "HuntTargetParity", placement, null, fleet);

    [Fact]
    public async Task CreateGame_WithACustomFleet_PlaysWithIt()
    {
        var response = await _client.PostAsJsonAsync(
            "/games", Request(["Carrier", "PatrolBoat", "PatrolBoat"]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var view = (await response.Content.ReadFromJsonAsync<GameViewResponse>())!;

        Assert.Equal(3, view.OwnFleet.Count);
        Assert.Equal(["Carrier", "PatrolBoat", "PatrolBoat"], [.. view.Fleet.Select(ship => ship.Kind)]);
        Assert.Equal([5, 1, 1], [.. view.Fleet.Select(ship => ship.Size)]);
    }

    [Fact]
    public async Task CreateGame_WithoutAFleet_KeepsTheClassicOne()
    {
        var response = await _client.PostAsJsonAsync("/games", Request(null));
        var view = (await response.Content.ReadFromJsonAsync<GameViewResponse>())!;

        Assert.Equal(5, view.OwnFleet.Count);
        Assert.Equal(
            ["Carrier", "Battleship", "Cruiser", "Submarine", "Destroyer"],
            [.. view.Fleet.Select(ship => ship.Kind)]);
    }

    [Theory]
    [InlineData("Frigate")]
    [InlineData("")]
    [InlineData("3")]
    public async Task CreateGame_WithAnUnknownShipKind_Returns400(string kind)
    {
        var response = await _client.PostAsJsonAsync("/games", Request(["Carrier", kind]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Trop de navires, un navire plus long que la grille, une grille trop
    /// remplie. Le statut seul ne prouverait rien : une flotte impossible finit
    /// <b>aussi</b> en 400 quand le placement aléatoire épuise ses essais. Ce qui
    /// distingue les deux chemins est la forme de la réponse — le filtre de
    /// validation produit un <c>ValidationProblem</c>, avec ses <c>errors</c> ;
    /// l'échec du placeur produit un <c>Problem</c> nu. C'est le refus
    /// déterministe qui est vérifié ici, pas le hasard du placeur.
    /// </summary>
    [Theory]
    [InlineData(4, new[] { "Carrier" })]
    [InlineData(10, new[] { "PatrolBoat", "PatrolBoat", "PatrolBoat", "PatrolBoat", "PatrolBoat", "PatrolBoat",
                            "PatrolBoat", "PatrolBoat", "PatrolBoat", "PatrolBoat", "PatrolBoat", "PatrolBoat",
                            "PatrolBoat", "PatrolBoat", "PatrolBoat", "PatrolBoat" })]
    [InlineData(8, new[] { "Carrier", "Carrier", "Carrier", "Carrier", "Carrier", "Carrier",
                           "Carrier", "Carrier", "Carrier", "Carrier", "Carrier", "Carrier" })]
    public async Task CreateGame_WithAFleetThatCannotFit_IsRefusedByValidation(int side, string[] fleet)
    {
        var response = await _client.PostAsJsonAsync("/games", Request(fleet, side));
        var problem = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"errors\"", problem);
        Assert.Contains("Fleet", problem);
    }

    /// <summary>
    /// Le placement manuel contrôle la composition <b>de la partie</b>, pas une
    /// flotte classique gravée dans le domaine — c'est la dette que l'ADR 0010
    /// avait notée pour cet item.
    /// </summary>
    [Fact]
    public async Task PlaceFleet_MustMatchTheCompositionOfTheGame()
    {
        var created = await _client.PostAsJsonAsync(
            "/games", Request(["Cruiser", "PatrolBoat"], placement: "Manual"));
        var view = (await created.Content.ReadFromJsonAsync<GameViewResponse>())!;

        Assert.Equal("AwaitingFleet", view.Status);
        Assert.Equal(["Cruiser", "PatrolBoat"], [.. view.FleetToPlace.Select(ship => ship.Kind)]);

        var classic = await _client.PutAsJsonAsync($"/games/{view.GameId}/fleet", new PlaceFleetRequest(
        [
            new("Carrier", 0, 0, "Vertical"),
            new("Battleship", 2, 0, "Vertical"),
            new("Cruiser", 4, 0, "Vertical"),
            new("Submarine", 6, 0, "Vertical"),
            new("Destroyer", 8, 0, "Vertical")
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, classic.StatusCode);

        var matching = await _client.PutAsJsonAsync($"/games/{view.GameId}/fleet", new PlaceFleetRequest(
        [
            new("Cruiser", 0, 0, "Vertical"),
            new("PatrolBoat", 5, 5, "Horizontal")
        ]));

        Assert.Equal(HttpStatusCode.OK, matching.StatusCode);
        Assert.Equal("InProgress", (await matching.Content.ReadFromJsonAsync<GameViewResponse>())!.Status);
    }

    [Fact]
    public async Task ACustomFleet_IsPlayableToTheEnd()
    {
        var created = await _client.PostAsJsonAsync("/games", Request(["PatrolBoat", "Destroyer"]));
        var view = (await created.Content.ReadFromJsonAsync<GameViewResponse>())!;

        string? winner = null;

        // Avec trois cases par flotte, le bot gagne souvent le premier : la fin
        // de partie peut tomber sur n'importe lequel des tirs de l'aller-retour.
        foreach (var index in Enumerable.Range(0, 100))
        {
            var round = await _client.PlayRoundAsync(view.GameId, index % 10, index / 10);

            if (round[^1].GameOver)
            {
                winner = round[^1].Winner;
                break;
            }
        }

        Assert.NotNull(winner);
        Assert.Contains(winner, new[] { "Olivier", "Bot" });
    }

    /// <summary>
    /// Le catalogue du front et l'énumération du serveur vivent dans deux projets
    /// qui ne se référencent pas. Ce test est le seul endroit où leur accord —
    /// noms <b>et longueurs</b> — est vérifié.
    /// </summary>
    [Fact]
    public void TheSharedShipCatalog_MatchesTheDomain()
    {
        Assert.Equal(
            BattleShip.Domain.EnumNames<BattleShip.Domain.ShipKind>.All.Order(),
            ShipCatalog.All.Select(ship => ship.Name).Order());

        foreach (var ship in ShipCatalog.All)
        {
            var kind = BattleShip.Domain.EnumNames<BattleShip.Domain.ShipKind>.Parse(ship.Name);
            Assert.Equal(BattleShip.Domain.ShipKindExtensions.Size(kind), ship.Size);
        }

        Assert.Equal(
            BattleShip.Domain.FleetTemplate.Standard.Select(kind => kind.ToString()),
            ShipCatalog.Classic);
    }
}
