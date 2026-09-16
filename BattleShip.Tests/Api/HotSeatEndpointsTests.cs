using System.Net;
using System.Net.Http.Json;
using BattleShip.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class HotSeatEndpointsTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateGameRequest LocalGame(string? opponent = "Ulysse") =>
        new("Olivier", 10, 10, "Local", "Random", "Random", opponent);

    private async Task<GameViewResponse> CreateLocalGameAsync()
    {
        var response = await _client.PostAsJsonAsync("/games", LocalGame());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GameViewResponse>())!;
    }

    [Fact]
    public async Task CreateGame_InLocalMode_StartsWithTheFirstPlayer()
    {
        var view = await CreateLocalGameAsync();

        Assert.Equal("Local", view.Mode);
        Assert.Equal("Olivier", view.ViewerName);
        Assert.True(view.IsViewerTurn);
        Assert.Equal("InProgress", view.Status);
        Assert.Equal(5, view.OwnFleet.Count);
    }

    /// <summary>
    /// Balaie la grille jusqu'au premier tir du résultat demandé, en retenant qui
    /// tirait. Le tireur change en cours de route : c'est précisément ce que les
    /// deux tests suivants mesurent.
    /// </summary>
    private async Task<(string Shooter, ShotOutcomeResponse Outcome)> FireUntilAsync(Guid gameId, string result)
    {
        var shooter = (await _client.GetFromJsonAsync<GameViewResponse>($"/games/{gameId}"))!.ViewerName;

        foreach (var index in Enumerable.Range(0, 100))
        {
            var outcome = await _client.FireAsync(gameId, index % 10, index / 10);

            Assert.False(outcome.GameOver, $"la partie s'est terminée avant le premier « {result} »");

            if (outcome.Result == result)
            {
                return (shooter, outcome);
            }

            shooter = outcome.View.ViewerName;
        }

        throw new InvalidOperationException($"aucun « {result} » en cent tirs.");
    }

    [Fact]
    public async Task Fire_InLocalMode_OnAMiss_HandsTheViewToTheOtherHuman()
    {
        var game = await CreateLocalGameAsync();

        var (shooter, outcome) = await FireUntilAsync(game.GameId, "Miss");

        Assert.NotEqual(shooter, outcome.View.ViewerName);
        Assert.True(outcome.View.IsViewerTurn);
    }

    /// <summary>
    /// Le pendant du test précédent, et le cas qui bloquait l'interface : sur une
    /// touche le tireur garde la main, donc la vue ne doit pas changer de joueur.
    /// Armer la passation ici laisserait le navigateur attendre un adversaire que
    /// le serveur n'appellera jamais. Voir AGENTS.md § 3 et l'ADR 0014.
    /// </summary>
    [Fact]
    public async Task Fire_InLocalMode_OnAHit_KeepsTheViewOnTheSameHuman()
    {
        var game = await CreateLocalGameAsync();

        var (shooter, outcome) = await FireUntilAsync(game.GameId, "Hit");

        Assert.Equal(shooter, outcome.View.ViewerName);
        Assert.True(outcome.View.IsViewerTurn);
    }

    /// <summary>
    /// La garantie du hot-seat côté serveur : chaque réponse ne porte qu'une
    /// flotte, celle du joueur décrit. L'écran de passation protège l'affichage,
    /// pas le réseau. Voir ADR 0003 et ADR 0011.
    /// </summary>
    [Fact]
    public async Task ASequenceOfShots_NeverServesTwoFleetsAtOnce()
    {
        var game = await CreateLocalGameAsync();
        var fleets = new Dictionary<string, HashSet<CoordinatesDto>>();

        for (var turn = 0; turn < 12; turn++)
        {
            var view = (await _client.FireAsync(game.GameId, turn % 10, turn / 10)).View;

            Assert.Equal(5, view.OwnFleet.Count);

            var served = view.OwnFleet.SelectMany(ship => ship.Cells).ToHashSet();

            if (fleets.TryGetValue(view.ViewerName, out var known))
            {
                Assert.True(known.SetEquals(served), $"la flotte servie à {view.ViewerName} a changé");
            }
            else
            {
                fleets[view.ViewerName] = served;
            }
        }

        Assert.Equal(2, fleets.Count);
        Assert.False(fleets["Olivier"].SetEquals(fleets["Ulysse"]),
            "les deux flottes sont identiques : le test ne prouverait rien");
    }

    [Fact]
    public async Task PlayBotTurn_InALocalGame_Returns409()
    {
        var game = await CreateLocalGameAsync();

        var response = await _client.PostAsJsonAsync($"/games/{game.GameId}/bot-turn", new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateGame_InLocalMode_WithoutAnOpponentName_Returns400(string? opponent)
    {
        var response = await _client.PostAsJsonAsync("/games", LocalGame(opponent));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// La règle est conditionnelle : en Solo l'adversaire est un bot que le
    /// serveur nomme lui-même, donc l'absence de nom n'est pas une erreur.
    /// </summary>
    [Fact]
    public async Task CreateGame_InSoloMode_NeedsNoOpponentName()
    {
        var response = await _client.PostAsJsonAsync(
            "/games", new CreateGameRequest("Olivier", 10, 10, "Solo", "Random", "Random"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var view = await response.Content.ReadFromJsonAsync<GameViewResponse>();
        Assert.Equal("Solo", view!.Mode);
    }

    [Theory]
    [InlineData("Online")]
    [InlineData("")]
    [InlineData("1")]
    public async Task CreateGame_WithAnUnknownMode_Returns400(string mode)
    {
        var response = await _client.PostAsJsonAsync(
            "/games", new CreateGameRequest("Olivier", 10, 10, mode, "Random", "Random", "Ulysse"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ALocalGame_WithManualPlacement_AsksEachHumanInTurn()
    {
        var created = await _client.PostAsJsonAsync(
            "/games", new CreateGameRequest("Olivier", 10, 10, "Local", "Random", "Manual", "Ulysse"));
        var view = (await created.Content.ReadFromJsonAsync<GameViewResponse>())!;

        Assert.Equal("AwaitingFleet", view.Status);
        Assert.Equal("Olivier", view.ViewerName);

        var fleet = new PlaceFleetRequest(
        [
            new("Carrier", 0, 0, "Vertical"),
            new("Battleship", 2, 0, "Vertical"),
            new("Cruiser", 4, 0, "Vertical"),
            new("Submarine", 6, 0, "Vertical"),
            new("Destroyer", 8, 0, "Vertical")
        ]);

        var afterFirst = await _client.PutAsJsonAsync($"/games/{view.GameId}/fleet", fleet);
        var second = (await afterFirst.Content.ReadFromJsonAsync<GameViewResponse>())!;

        Assert.Equal("AwaitingFleet", second.Status);
        Assert.Equal("Ulysse", second.ViewerName);
        Assert.Equal(5, second.FleetToPlace.Count);
        Assert.Empty(second.OwnFleet);

        var afterSecond = await _client.PutAsJsonAsync($"/games/{view.GameId}/fleet", fleet);
        var started = (await afterSecond.Content.ReadFromJsonAsync<GameViewResponse>())!;

        Assert.Equal("InProgress", started.Status);
        Assert.Equal("Olivier", started.ViewerName);
    }

    [Fact]
    public async Task TheView_NamesTheOpponent_AndCarriesNothingElseAboutHim()
    {
        var view = await CreateLocalGameAsync();

        Assert.Equal("Olivier", view.ViewerName);
        Assert.Equal("Ulysse", view.OpponentName);

        // La seule chose que la réponse dit de l'adversaire est son nom : le type
        // ne porte aucun membre capable de transporter sa flotte. Voir ADR 0003.
        Assert.Equal(5, view.OwnFleet.Count);
        Assert.Empty(view.ShotsFired);
        Assert.Empty(view.ShotsReceived);
    }
}
