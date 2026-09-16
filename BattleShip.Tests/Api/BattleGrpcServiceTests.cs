using System.Net;
using System.Net.Http.Json;
using BattleShip.Grpc;
using BattleShip.Models;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class BattleGrpcServiceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>
    /// TestServer ne renseigne pas la version de reponse attendue par le client
    /// gRPC ; ce relais la recopie depuis la requete.
    /// </summary>
    private sealed class ResponseVersionHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            response.Version = request.Version;
            return response;
        }
    }

    private Battle.BattleClient CreateGrpcClient()
    {
        var http = factory.CreateDefaultClient(new ResponseVersionHandler());
        var channel = GrpcChannel.ForAddress(
            factory.ClientOptions.BaseAddress,
            new GrpcChannelOptions { HttpClient = http });

        return new Battle.BattleClient(channel);
    }

    /// <summary>
    /// Le pendant gRPC du balayage HTTP : le seul moment ou la main passe au bot
    /// est le premier coup manque. Voir AGENTS.md § 3.
    /// </summary>
    private static async Task FireUntilTheBotHasTheHandAsync(Battle.BattleClient grpc, Guid gameId)
    {
        foreach (var index in Enumerable.Range(0, 100))
        {
            var outcome = await grpc.FireAsync(new FireCommand
            {
                GameId = gameId.ToString(),
                Column = index % 10,
                Row = index / 10
            });

            Assert.False(outcome.GameOver, "la partie s'est terminée avant que la main ne passe au bot");

            if (outcome.Result is "Miss")
            {
                return;
            }
        }

        throw new InvalidOperationException("aucun coup manqué en cent tirs.");
    }

    private async Task<Guid> CreateGameAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/games", new CreateGameRequest("Olivier", 10, 10, "Solo", "Random", "Random"));
        response.EnsureSuccessStatusCode();
        var view = await response.Content.ReadFromJsonAsync<GameViewResponse>();
        return view!.GameId;
    }

    [Fact]
    public async Task Fire_OverGrpc_ReturnsTheOutcomeOfTheShot()
    {
        var http = factory.CreateClient();
        var gameId = await CreateGameAsync(http);

        var outcome = await CreateGrpcClient().FireAsync(new FireCommand
        {
            GameId = gameId.ToString(),
            Column = 4,
            Row = 4
        });

        Assert.Equal(4, outcome.Column);
        Assert.Equal(4, outcome.Row);
        Assert.Contains(outcome.Result, new[] { "Miss", "Hit", "Sunk" });
    }

    [Fact]
    public async Task Fire_OverGrpc_OnAnUnknownGame_ReportsNotFound()
    {
        var error = await Assert.ThrowsAsync<RpcException>(() =>
            CreateGrpcClient().FireAsync(new FireCommand
            {
                GameId = Guid.NewGuid().ToString(),
                Column = 0,
                Row = 0
            }).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, error.StatusCode);
    }

    [Fact]
    public async Task Fire_OverGrpc_OutsideTheBoard_ReportsInvalidArgument()
    {
        var http = factory.CreateClient();
        var gameId = await CreateGameAsync(http);

        var error = await Assert.ThrowsAsync<RpcException>(() =>
            CreateGrpcClient().FireAsync(new FireCommand
            {
                GameId = gameId.ToString(),
                Column = 10,
                Row = 0
            }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
    }

    [Fact]
    public async Task Fire_OverGrpc_WithANegativeCoordinate_IsStoppedByTheInterceptor()
    {
        var http = factory.CreateClient();
        var gameId = await CreateGameAsync(http);

        var error = await Assert.ThrowsAsync<RpcException>(() =>
            CreateGrpcClient().FireAsync(new FireCommand
            {
                GameId = gameId.ToString(),
                Column = -1,
                Row = 0
            }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
    }

    [Fact]
    public async Task Fire_OverGrpc_OnACellAlreadyTargeted_ReportsFailedPrecondition()
    {
        var http = factory.CreateClient();
        var gameId = await CreateGameAsync(http);
        var grpc = CreateGrpcClient();

        await grpc.FireAsync(new FireCommand { GameId = gameId.ToString(), Column = 3, Row = 3 });

        // Rendre la main avant de rejouer la case : sinon le refus obtenu serait
        // « ce n'est pas a vous de jouer », qui porte le meme statut gRPC.
        await http.PlayBotTurnsAsync(gameId);

        var error = await Assert.ThrowsAsync<RpcException>(() =>
            grpc.FireAsync(new FireCommand
            {
                GameId = gameId.ToString(),
                Column = 3,
                Row = 3
            }).ResponseAsync);

        Assert.Equal(StatusCode.FailedPrecondition, error.StatusCode);
    }

    [Fact]
    public async Task Fire_OverGrpc_WhileItIsTheBotTurn_ReportsFailedPrecondition()
    {
        // Le chemin gRPC porte le meme garde que HTTP : le client ne joue pas
        // le tour du bot, quel que soit le transport. Voir ADR 0003 et 0005.
        var http = factory.CreateClient();
        var gameId = await CreateGameAsync(http);
        var grpc = CreateGrpcClient();

        // La main ne passe qu'au coup manque : tirer une fois ne suffit plus a
        // mettre la partie dans l'etat que ce test interroge.
        await FireUntilTheBotHasTheHandAsync(grpc, gameId);

        var error = await Assert.ThrowsAsync<RpcException>(() =>
            grpc.FireAsync(new FireCommand
            {
                GameId = gameId.ToString(),
                Column = 9,
                Row = 9
            }).ResponseAsync);

        Assert.Equal(StatusCode.FailedPrecondition, error.StatusCode);

        var botTurn = await http.PostAsJsonAsync($"/games/{gameId}/bot-turn", new { });
        Assert.Equal(HttpStatusCode.OK, botTurn.StatusCode);
    }

    [Fact]
    public async Task Fire_OverGrpcThenOverHttpOnTheSameCell_IsRefusedByBothTransports()
    {
        // Les deux chemins delegent au meme appel du domaine : une regle
        // reimplementee dans le service gRPC ferait echouer ce test.
        // Voir ADR 0005.
        var http = factory.CreateClient();
        var gameId = await CreateGameAsync(http);

        await CreateGrpcClient().FireAsync(new FireCommand { GameId = gameId.ToString(), Column = 6, Row = 2 });
        await http.PlayBotTurnsAsync(gameId);

        var overHttp = await http.PostAsJsonAsync($"/games/{gameId}/shots", new FireRequest(6, 2));

        Assert.Equal(HttpStatusCode.Conflict, overHttp.StatusCode);
    }
}
