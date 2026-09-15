using System.Net;
using System.Net.Http.Json;
using BattleShip.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class GameEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateGameRequest ValidRequest => new("Olivier", 10, 10, "Random");

    private async Task<GameViewResponse> CreateGameAsync()
    {
        var response = await _client.PostAsJsonAsync("/games", ValidRequest);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GameViewResponse>())!;
    }

    [Fact]
    public async Task CreateGame_WithAValidRequest_Returns201AndDescribesTheHuman()
    {
        var response = await _client.PostAsJsonAsync("/games", ValidRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var view = await response.Content.ReadFromJsonAsync<GameViewResponse>();
        Assert.Equal("Olivier", view!.ViewerName);
        Assert.True(view.IsViewerTurn);
        Assert.Equal(5, view.OwnFleet.Count);
        Assert.Empty(view.ShotsFired);
    }

    [Theory]
    [InlineData("", 10, 10, "Random")]
    [InlineData("Olivier", 3, 10, "Random")]
    [InlineData("Olivier", 10, 99, "Random")]
    [InlineData("Olivier", 10, 10, "Expert")]
    [InlineData("Olivier", 10, 10, "")]
    // Enum.TryParse accepterait la valeur numerique sous-jacente : la valider
    // ainsi laisserait passer un niveau qui n'existe pas. Voir BotDifficulties.
    [InlineData("Olivier", 10, 10, "42")]
    public async Task CreateGame_WithAnInvalidRequest_Returns400(string name, int columns, int rows, string difficulty)
    {
        // Aucun de ces endpoints n'appelle ValidateAsync : si le filtre generique
        // n'etait pas branche, ces requetes passeraient. Voir ADR 0006.
        var response = await _client.PostAsJsonAsync("/games", new CreateGameRequest(name, columns, rows, difficulty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetGame_ForAnUnknownIdentifier_Returns404()
    {
        var response = await _client.GetAsync($"/games/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Fire_OnAValidCell_Returns200AndReportsTheTarget()
    {
        var game = await CreateGameAsync();

        var response = await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(4, 4));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var outcome = await response.Content.ReadFromJsonAsync<ShotOutcomeResponse>();
        Assert.Equal(new CoordinatesDto(4, 4), outcome!.Target);
        Assert.Contains(outcome.Result, new[] { "Miss", "Hit", "Sunk" });
    }

    [Fact]
    public async Task Fire_OnACellAlreadyTargeted_Returns409()
    {
        var game = await CreateGameAsync();
        await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(4, 4));
        await _client.PostAsJsonAsync($"/games/{game.GameId}/bot-turn", new { });

        var response = await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(4, 4));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Fire_OutsideTheBoard_Returns400()
    {
        var game = await CreateGameAsync();

        var response = await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(10, 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Fire_WithANegativeCoordinate_Returns400()
    {
        var game = await CreateGameAsync();

        var response = await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(-1, 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Fire_OnAnUnknownGame_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/games/{Guid.NewGuid()}/shots", new FireRequest(0, 0));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Fire_WhileItIsTheBotTurn_Returns409AndLeavesTheBotTurnToPlay()
    {
        // Sans ce refus, le second POST /shots ferait tirer le bot sur une case
        // choisie par le client, et consommerait son tour. Voir ADR 0003.
        var game = await CreateGameAsync();
        await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(4, 4));

        var response = await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(5, 5));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var botTurn = await _client.PostAsJsonAsync($"/games/{game.GameId}/bot-turn", new { });
        Assert.Equal(HttpStatusCode.OK, botTurn.StatusCode);
    }

    [Fact]
    public async Task BotTurn_WhileItIsTheHumanTurn_Returns409()
    {
        var game = await CreateGameAsync();

        var response = await _client.PostAsJsonAsync($"/games/{game.GameId}/bot-turn", new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetGame_AfterTheHumanFired_StillDescribesTheHuman()
    {
        // Le tour est passe au bot, mais le serveur ne sert jamais la vue d'un
        // bot au navigateur. Voir ADR 0003.
        var game = await CreateGameAsync();
        await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(0, 0));

        var view = await _client.GetFromJsonAsync<GameViewResponse>($"/games/{game.GameId}");

        Assert.Equal("Olivier", view!.ViewerName);
        Assert.False(view.IsViewerTurn);
    }

    [Fact]
    public async Task FullGame_PlayedThroughTheApi_ReachesAWinner()
    {
        var game = await CreateGameAsync();
        var fired = new HashSet<CoordinatesDto>();
        ShotOutcomeResponse? last = null;

        for (var column = 0; column < 10 && last?.GameOver is not true; column++)
        {
            for (var row = 0; row < 10 && last?.GameOver is not true; row++)
            {
                var shot = await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(column, row));
                Assert.Equal(HttpStatusCode.OK, shot.StatusCode);

                last = await shot.Content.ReadFromJsonAsync<ShotOutcomeResponse>();
                fired.Add(new CoordinatesDto(column, row));

                // La vue ne revele sur l'adversaire que les cases effectivement visees.
                Assert.All(last!.View.ShotsFired, cell => Assert.Contains(cell.Target, fired));

                if (last.GameOver) break;

                var botTurn = await _client.PostAsJsonAsync($"/games/{game.GameId}/bot-turn", new { });
                Assert.Equal(HttpStatusCode.OK, botTurn.StatusCode);
                last = await botTurn.Content.ReadFromJsonAsync<ShotOutcomeResponse>();
            }
        }

        Assert.True(last!.GameOver, "la partie ne s'est pas terminée en 100 tours");
        Assert.False(string.IsNullOrWhiteSpace(last.Winner));
    }

    [Fact]
    public async Task Fire_OnceTheGameIsOver_Returns409()
    {
        var game = await CreateGameAsync();
        ShotOutcomeResponse? last = null;

        for (var column = 0; column < 10 && last?.GameOver is not true; column++)
        {
            for (var row = 0; row < 10 && last?.GameOver is not true; row++)
            {
                var shot = await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(column, row));
                last = await shot.Content.ReadFromJsonAsync<ShotOutcomeResponse>();
                if (last!.GameOver) break;
                var botTurn = await _client.PostAsJsonAsync($"/games/{game.GameId}/bot-turn", new { });
                last = await botTurn.Content.ReadFromJsonAsync<ShotOutcomeResponse>();
            }
        }

        var afterTheEnd = await _client.PostAsJsonAsync($"/games/{game.GameId}/shots", new FireRequest(9, 9));

        Assert.Equal(HttpStatusCode.Conflict, afterTheEnd.StatusCode);
    }
}
