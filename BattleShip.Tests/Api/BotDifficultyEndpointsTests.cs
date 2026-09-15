using System.Net;
using System.Net.Http.Json;
using BattleShip.Domain;
using BattleShip.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BattleShip.Tests.Api;

public class BotDifficultyEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class RecordingStrategyFactory : IBotStrategyFactory
    {
        private readonly BotStrategyFactory _real = new(new Random(1));

        public List<BotDifficulty> Requested { get; } = [];

        public IBotStrategy For(BotDifficulty level)
        {
            Requested.Add(level);

            return _real.For(level);
        }
    }

    [Theory]
    [InlineData("Random")]
    [InlineData("HuntTarget")]
    [InlineData("HuntTargetParity")]
    public async Task CreateGame_EchoesTheLevelItWasGiven(string level)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/games", new CreateGameRequest("Olivier", 10, 10, level));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var view = await response.Content.ReadFromJsonAsync<GameViewResponse>();
        Assert.Equal(level, view!.BotDifficulty);
    }

    /// <summary>
    /// Sans ce contrôle, le niveau pourrait être stocké, renvoyé au client et
    /// pourtant ignoré au moment où le bot joue : le seul témoin utile est la
    /// fabrique, à qui l'endpoint doit réclamer ce niveau-là.
    /// </summary>
    [Fact]
    public async Task PlayBotTurn_AsksTheFactoryForTheLevelChosenAtCreation()
    {
        var strategies = new RecordingStrategyFactory();

        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                services => services.AddSingleton<IBotStrategyFactory>(strategies)))
            .CreateClient();

        var created = await client.PostAsJsonAsync(
            "/games", new CreateGameRequest("Olivier", 10, 10, "HuntTargetParity"));
        var view = await created.Content.ReadFromJsonAsync<GameViewResponse>();

        await client.PostAsJsonAsync($"/games/{view!.GameId}/shots", new FireRequest(0, 0));
        var botTurn = await client.PostAsJsonAsync($"/games/{view.GameId}/bot-turn", new { });

        Assert.Equal(HttpStatusCode.OK, botTurn.StatusCode);
        Assert.Equal([BotDifficulty.HuntTargetParity], strategies.Requested);
    }

    [Fact]
    public async Task CreateGame_WithAnUnknownLevel_NamesTheAcceptedOnes()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/games", new CreateGameRequest("Olivier", 10, 10, "Expert"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("HuntTargetParity", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Le catalogue proposé par le front et l'énumération acceptée par le
    /// serveur vivent dans deux projets qui ne se référencent pas. Ce test est
    /// le seul endroit où leur accord est vérifié.
    /// </summary>
    [Fact]
    public void TheSharedCatalog_NamesEveryLevelOfTheDomain_AndNothingElse() =>
        Assert.Equal(
            BotDifficulties.Names.Order(),
            BotDifficultyCatalog.All.Select(option => option.Name).Order());
}
