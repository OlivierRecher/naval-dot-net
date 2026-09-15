using System.Net;
using System.Net.Http.Json;
using BattleShip.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class FleetPlacementEndpointsTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateGameRequest Manual => new("Olivier", 10, 10, "Solo", "Random", "Manual");

    private static List<ShipPlacementDto> ValidFleet() =>
    [
        new("Carrier", 0, 0, "Vertical"),
        new("Battleship", 2, 0, "Vertical"),
        new("Cruiser", 4, 0, "Vertical"),
        new("Submarine", 6, 0, "Vertical"),
        new("Destroyer", 8, 0, "Vertical")
    ];

    private async Task<GameViewResponse> CreateManualGameAsync()
    {
        var response = await _client.PostAsJsonAsync("/games", Manual);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GameViewResponse>())!;
    }

    [Fact]
    public async Task CreateGame_WithManualPlacement_AwaitsTheFleetAndAnnouncesWhatToPlace()
    {
        var view = await CreateManualGameAsync();

        Assert.Equal("AwaitingFleet", view.Status);
        Assert.Empty(view.OwnFleet);
        Assert.Equal(5, view.FleetToPlace.Count);
        Assert.Equal([5, 4, 3, 3, 2], [.. view.FleetToPlace.Select(ship => ship.Size)]);
    }

    [Fact]
    public async Task CreateGame_WithRandomPlacement_StartsImmediately()
    {
        var response = await _client.PostAsJsonAsync("/games", new CreateGameRequest("Olivier", 10, 10, "Solo", "Random", "Random"));
        var view = await response.Content.ReadFromJsonAsync<GameViewResponse>();

        Assert.Equal("InProgress", view!.Status);
        Assert.Equal(5, view.OwnFleet.Count);
        Assert.Empty(view.FleetToPlace);
    }

    [Fact]
    public async Task PlaceFleet_WithAValidFleet_StartsTheGame()
    {
        var game = await CreateManualGameAsync();

        var response = await _client.PutAsJsonAsync($"/games/{game.GameId}/fleet", new PlaceFleetRequest(ValidFleet()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var view = await response.Content.ReadFromJsonAsync<GameViewResponse>();
        Assert.Equal("InProgress", view!.Status);
        Assert.Equal(5, view.OwnFleet.Count);
        Assert.Empty(view.FleetToPlace);
        Assert.True(view.IsViewerTurn);
    }

    [Fact]
    public async Task PlaceFleet_WithOverlappingShips_Returns400()
    {
        var game = await CreateManualGameAsync();
        var fleet = ValidFleet();
        fleet[1] = fleet[1] with { Column = 0 };

        var response = await _client.PutAsJsonAsync($"/games/{game.GameId}/fleet", new PlaceFleetRequest(fleet));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PlaceFleet_WithAShipLeavingTheBoard_Returns400()
    {
        var game = await CreateManualGameAsync();
        var fleet = ValidFleet();
        fleet[0] = fleet[0] with { Row = 9 };

        var response = await _client.PutAsJsonAsync($"/games/{game.GameId}/fleet", new PlaceFleetRequest(fleet));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PlaceFleet_WithAnIncompleteFleet_Returns400()
    {
        var game = await CreateManualGameAsync();
        var fleet = ValidFleet();
        fleet.RemoveAt(0);

        var response = await _client.PutAsJsonAsync($"/games/{game.GameId}/fleet", new PlaceFleetRequest(fleet));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PlaceFleet_Twice_Returns409()
    {
        var game = await CreateManualGameAsync();
        await _client.PutAsJsonAsync($"/games/{game.GameId}/fleet", new PlaceFleetRequest(ValidFleet()));

        var response = await _client.PutAsJsonAsync($"/games/{game.GameId}/fleet", new PlaceFleetRequest(ValidFleet()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PlaceFleet_OnAGameWhoseFleetWasPlacedAtRandom_Returns409()
    {
        var created = await _client.PostAsJsonAsync("/games", new CreateGameRequest("Olivier", 10, 10, "Solo", "Random", "Random"));
        var game = await created.Content.ReadFromJsonAsync<GameViewResponse>();

        var response = await _client.PutAsJsonAsync($"/games/{game!.GameId}/fleet", new PlaceFleetRequest(ValidFleet()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PlaceFleet_OnAnUnknownGame_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/games/{Guid.NewGuid()}/fleet", new PlaceFleetRequest(ValidFleet()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Produit par le filtre de validation, pas par le domaine : un type de
    /// navire inconnu n'atteint jamais <c>ShipPlacement</c>. Voir ADR 0006.
    /// </summary>
    [Theory]
    [InlineData("Frigate", "Vertical")]
    [InlineData("Carrier", "Diagonal")]
    [InlineData("", "Vertical")]
    public async Task PlaceFleet_WithAnUnknownKindOrOrientation_Returns400(string kind, string orientation)
    {
        var game = await CreateManualGameAsync();
        var fleet = ValidFleet();
        fleet[0] = new ShipPlacementDto(kind, 0, 0, orientation);

        var response = await _client.PutAsJsonAsync($"/games/{game.GameId}/fleet", new PlaceFleetRequest(fleet));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PlaceFleet_WithNoShipAtAll_Returns400()
    {
        var game = await CreateManualGameAsync();

        var response = await _client.PutAsJsonAsync($"/games/{game.GameId}/fleet", new PlaceFleetRequest([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Fire_BeforeTheFleetIsPlaced_Returns409()
    {
        var game = await CreateManualGameAsync();

        var response = await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(0, 0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PlayBotTurn_BeforeTheFleetIsPlaced_Returns409()
    {
        var game = await CreateManualGameAsync();

        var response = await _client.PostAsJsonAsync($"/games/{game.GameId}/bot-turn", new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("Guided")]
    [InlineData("")]
    [InlineData("42")]
    public async Task CreateGame_WithAnUnknownFleetPlacement_Returns400(string placement)
    {
        var response = await _client.PostAsJsonAsync("/games", new CreateGameRequest("Olivier", 10, 10, "Solo", "Random", placement));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// La liste des navires à poser décrit une composition, pas des positions :
    /// elle ne peut rien révéler de la flotte adverse. Voir ADR 0003.
    /// </summary>
    [Fact]
    public async Task TheFleetToPlace_DescribesTheCompositionOnly()
    {
        var view = await CreateManualGameAsync();

        Assert.Equal(
            ["Carrier", "Battleship", "Cruiser", "Submarine", "Destroyer"],
            [.. view.FleetToPlace.Select(ship => ship.Kind)]);
    }
}
