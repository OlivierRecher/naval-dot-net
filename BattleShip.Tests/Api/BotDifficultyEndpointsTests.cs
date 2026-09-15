using System.Net;
using System.Net.Http.Json;
using System.Text;
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

        public IBotStrategy For(BotDifficulty difficulty)
        {
            Requested.Add(difficulty);

            return _real.For(difficulty);
        }
    }

    [Theory]
    [InlineData("Random")]
    [InlineData("HuntTarget")]
    [InlineData("HuntTargetParity")]
    public async Task CreateGame_EchoesTheDifficultyItWasGiven(string difficulty)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/games", new CreateGameRequest("Olivier", 10, 10, difficulty, "Random"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var view = await response.Content.ReadFromJsonAsync<GameViewResponse>();
        Assert.Equal(difficulty, view!.BotDifficulty);
    }

    /// <summary>
    /// Sans ce contrôle, le niveau pourrait être stocké, renvoyé au client et
    /// pourtant ignoré au moment où le bot joue : le seul témoin utile est la
    /// fabrique, à qui l'endpoint doit réclamer ce niveau-là.
    /// </summary>
    [Fact]
    public async Task PlayBotTurn_AsksTheFactoryForTheDifficultyChosenAtCreation()
    {
        var strategies = new RecordingStrategyFactory();

        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                services => services.AddSingleton<IBotStrategyFactory>(strategies)))
            .CreateClient();

        var created = await client.PostAsJsonAsync(
            "/games", new CreateGameRequest("Olivier", 10, 10, "HuntTargetParity", "Random"));
        var view = await created.Content.ReadFromJsonAsync<GameViewResponse>();

        await client.PostAsJsonAsync($"/games/{view!.GameId}/shots", new FireRequest(0, 0));
        var botTurn = await client.PostAsJsonAsync($"/games/{view.GameId}/bot-turn", new { });

        Assert.Equal(HttpStatusCode.OK, botTurn.StatusCode);
        Assert.Equal([BotDifficulty.HuntTargetParity], strategies.Requested);
    }

    [Fact]
    public async Task CreateGame_WithAnUnknownDifficulty_NamesTheAcceptedOnes()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/games", new CreateGameRequest("Olivier", 10, 10, "Expert", "Random"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("HuntTargetParity", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Le nom voyage en texte pour que le filtre de validation puisse refuser une
    /// valeur inconnue (ADR 0006). Cela ne vaut que pour une **chaîne** JSON : un
    /// nombre, un booléen ou un tableau échouent à la liaison, en amont du
    /// filtre. Le statut reste 400, la forme de la réponse change.
    /// </summary>
    [Theory]
    [InlineData("2")]
    [InlineData("true")]
    [InlineData("""["HuntTarget"]""")]
    public async Task CreateGame_WithANonTextualDifficulty_IsRefusedByTheBinderNotTheValidator(string raw)
    {
        var client = factory.CreateClient();
        var body = new StringContent(
            $$"""{"playerName":"Olivier","columns":10,"rows":10,"botDifficulty":{{raw}}}""",
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/games", body);
        var problem = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("BadHttpRequestException", problem);
        Assert.DoesNotContain("errors", problem);
    }

    /// <summary>
    /// Le nom est reconnu sans égard à la casse, mais la partie retient la valeur
    /// canonique : c'est elle que le client reçoit, et c'est elle que le catalogue
    /// du front sait traduire en libellé.
    /// </summary>
    [Fact]
    public async Task CreateGame_WithADifficultyInAnotherCase_StoresTheCanonicalName()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/games", new CreateGameRequest("Olivier", 10, 10, "hunttargetparity", "Random"));
        var view = await response.Content.ReadFromJsonAsync<GameViewResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("HuntTargetParity", view!.BotDifficulty);
    }

    /// <summary>
    /// Le catalogue proposé par le front et l'énumération acceptée par le
    /// serveur vivent dans deux projets qui ne se référencent pas. Ce test est
    /// le seul endroit où leur accord est vérifié.
    /// </summary>
    [Fact]
    public void TheSharedCatalog_NamesEveryDifficultyOfTheDomain_AndNothingElse() =>
        Assert.Equal(
            EnumNames<BotDifficulty>.All.Order(),
            BotDifficultyCatalog.All.Select(option => option.Name).Order());
}
