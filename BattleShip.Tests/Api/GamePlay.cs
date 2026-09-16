using System.Net;
using System.Net.Http.Json;
using BattleShip.Models;

namespace BattleShip.Tests.Api;

/// <summary>
/// Depuis qu'une touche laisse la main au tireur (AGENTS.md § 3), « l'humain a
/// tiré » ne veut plus dire « c'est au bot de jouer ». Les tests d'intégration
/// conduisent donc la partie comme le navigateur : ils lisent la vue pour savoir
/// à qui est le tour, au lieu d'alterner en aveugle — une alternance supposée
/// leur ferait réclamer un tour de bot refusé, et le refus passerait inaperçu.
/// </summary>
internal static class GamePlay
{
    internal static async Task<ShotOutcomeResponse> FireAsync(this HttpClient client, Guid gameId, int column, int row)
    {
        var response = await client.PostAsJsonAsync($"/games/{gameId}/shots", new FireRequest(column, row));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<ShotOutcomeResponse>())!;
    }

    /// <summary>
    /// Le tir du client puis ceux du bot tant qu'il garde la main, dans l'ordre.
    /// La liste vaut compte rendu : sa longueur est le nombre de tirs acceptés,
    /// son dernier élément l'état de la partie au moment où le client reprend.
    /// </summary>
    internal static async Task<IReadOnlyList<ShotOutcomeResponse>> PlayRoundAsync(
        this HttpClient client,
        Guid gameId,
        int column,
        int row)
    {
        var shots = new List<ShotOutcomeResponse> { await client.FireAsync(gameId, column, row) };

        shots.AddRange(await client.PlayBotTurnsAsync(gameId));

        return shots;
    }

    /// <summary>Rend la main au client : le bot enchaîne tant qu'il touche.</summary>
    internal static async Task<IReadOnlyList<ShotOutcomeResponse>> PlayBotTurnsAsync(this HttpClient client, Guid gameId)
    {
        var shots = new List<ShotOutcomeResponse>();

        while (await client.GetFromJsonAsync<GameViewResponse>($"/games/{gameId}")
               is { Status: "InProgress", IsViewerTurn: false })
        {
            var response = await client.PostAsJsonAsync($"/games/{gameId}/bot-turn", new { });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            shots.Add((await response.Content.ReadFromJsonAsync<ShotOutcomeResponse>())!);
        }

        return shots;
    }

    /// <summary>
    /// Balaie la grille jusqu'au premier coup manqué : c'est le seul moment où
    /// la main passe, donc le seul état depuis lequel un test peut affirmer que
    /// c'est au bot de jouer.
    /// </summary>
    internal static async Task<ShotOutcomeResponse> FireUntilTheBotHasTheHandAsync(this HttpClient client, Guid gameId)
    {
        foreach (var index in Enumerable.Range(0, 100))
        {
            var outcome = await client.FireAsync(gameId, index % 10, index / 10);

            Assert.False(outcome.GameOver, "la partie s'est terminée avant que la main ne passe au bot");

            if (outcome.Result is "Miss")
            {
                return outcome;
            }
        }

        throw new InvalidOperationException("aucun coup manqué en cent tirs : la grille était pleine de navires.");
    }
}
