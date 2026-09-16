using System.Net;
using System.Net.Http.Json;
using BattleShip.API.Persistence;
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
public class GameRepositorySubstitutionTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
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

        public int Saves { get; private set; }

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

        public void Save(Game game) => Saves++;
    }

    /// <summary>
    /// Le dépôt qui ne peut pas reconstruire ce qu'il a stocké : c'est ce que
    /// devient un journal écrit sous d'autres règles. Voir ADR 0014.
    /// </summary>
    private sealed class UnreplayableGameRepository : IGameRepository
    {
        public void Add(Game game) { }

        public Game? Find(Guid id) =>
            throw new UnreplayableJournalException(id, new InvalidOperationException("journal incoherent"));

        public void Save(Game game) { }
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

        var created = await client.PostAsJsonAsync("/games", new CreateGameRequest("Olivier", 10, 10, "Solo", "HuntTarget", "Random"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var view = await created.Content.ReadFromJsonAsync<GameViewResponse>();

        var read = await client.GetAsync($"/games/{view!.GameId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        // Le nombre de tirs qu'il faut pour rendre la main au bot depend du
        // hasard des flottes : on compte donc ce que le tour du bot ajoute, pas
        // un total.
        await client.FireUntilTheBotHasTheHandAsync(view.GameId);

        var lookups = repository.Lookups;
        var saves = repository.Saves;

        var botTurn = await client.PostAsJsonAsync($"/games/{view.GameId}/bot-turn", new { });
        Assert.Equal(HttpStatusCode.OK, botTurn.StatusCode);

        // La partie a bien transité par NOTRE dépôt, pas par celui de production.
        Assert.Equal(1, repository.Added);
        Assert.Equal(lookups + 1, repository.Lookups);

        // Une mutation, une validation. Ce compteur est la trace du point que
        // l'ADR 0004 n'avait pas prévu : un dépôt en mémoire n'en a pas besoin,
        // un dépôt persistant ne peut pas s'en passer. Voir REVUE-IA revue 8.
        Assert.Equal(saves + 1, repository.Saves);
    }

    /// <summary>
    /// Une partie que le dépôt ne sait plus rejouer se refuse, avec un message :
    /// sans ce filtre, un GET anodin remonterait en erreur serveur, et la cause
    /// resterait invisible au joueur comme au binôme. Voir ADR 0014.
    /// </summary>
    [Fact]
    public async Task GetGame_WhenTheJournalNoLongerReplays_Returns409()
    {
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                services => services.AddSingleton<IGameRepository>(new UnreplayableGameRepository())))
            .CreateClient();

        var response = await client.GetAsync($"/games/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
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
