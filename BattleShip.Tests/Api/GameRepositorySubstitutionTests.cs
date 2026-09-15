using System.Net;
using System.Net.Http.Json;
using BattleShip.Domain;
using BattleShip.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BattleShip.Tests.Api;

/// <summary>
/// Contrôle annoncé par l'ADR 0004 : l'abstraction du stockage ne vaut que si
/// une autre implémentation se substitue sans qu'aucun endpoint ne change.
/// C'est ce test qui distingue une abstraction utile d'une intention.
/// </summary>
public class GameRepositorySubstitutionTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>
    /// Volontairement NON concurrente et distincte de l'implémentation de
    /// production : si un endpoint dépendait du type concret plutôt que de
    /// l'interface, il ne la recevrait pas.
    /// </summary>
    private sealed class RecordingGameRepository : IGameRepository
    {
        private readonly Dictionary<Guid, Game> _games = [];

        public int Added { get; private set; }

        public int Lookups { get; private set; }

        public void Add(Game game)
        {
            Added++;
            _games[game.Id] = game;
        }

        public Game? Find(Guid id)
        {
            Lookups++;
            return _games.GetValueOrDefault(id);
        }
    }

    private (HttpClient Client, RecordingGameRepository Repository) SubstitutedHost()
    {
        var repository = new RecordingGameRepository();

        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                services => services.AddSingleton<IGameRepository>(repository)))
            .CreateClient();

        return (client, repository);
    }

    [Fact]
    public async Task Endpoints_RunAgainstASubstitutedRepository_WithoutAnyEndpointChange()
    {
        var (client, repository) = SubstitutedHost();

        var created = await client.PostAsJsonAsync("/games", new CreateGameRequest("Olivier", 10, 10));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var view = await created.Content.ReadFromJsonAsync<GameViewResponse>();

        var read = await client.GetAsync($"/games/{view!.GameId}");
        var shot = await client.PostAsJsonAsync($"/games/{view.GameId}/shots", new FireRequest(0, 0));
        var botTurn = await client.PostAsJsonAsync($"/games/{view.GameId}/bot-turn", new { });

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, shot.StatusCode);
        Assert.Equal(HttpStatusCode.OK, botTurn.StatusCode);

        // La partie a bien transité par NOTRE dépôt, pas par celui de production.
        Assert.Equal(1, repository.Added);
        Assert.Equal(3, repository.Lookups);
    }

    [Fact]
    public async Task GetGame_WhenTheSubstitutedRepositoryKnowsNothing_Returns404()
    {
        var (client, repository) = SubstitutedHost();

        var response = await client.GetAsync($"/games/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, repository.Added);
        Assert.Equal(1, repository.Lookups);
    }
}
