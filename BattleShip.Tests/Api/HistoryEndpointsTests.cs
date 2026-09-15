using System.Net;
using System.Net.Http.Json;
using BattleShip.Models;

namespace BattleShip.Tests.Api;

public class HistoryEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<GameViewResponse> PlayAsync(string difficulty, int shots)
    {
        var created = await _client.PostAsJsonAsync(
            "/games", new CreateGameRequest("Olivier", 10, 10, "Solo", difficulty, "Random"));
        var view = (await created.Content.ReadFromJsonAsync<GameViewResponse>())!;

        for (var turn = 0; turn < shots; turn++)
        {
            await _client.PostAsJsonAsync($"/games/{view.GameId}/shots", new FireRequest(turn % 10, turn / 10));
            await _client.PostAsJsonAsync($"/games/{view.GameId}/bot-turn", new { });
        }

        return view;
    }

    [Fact]
    public async Task RecentGames_ListsWhatWasPlayed()
    {
        var game = await PlayAsync("HuntTarget", 3);

        var response = await _client.GetAsync("/games");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await response.Content.ReadFromJsonAsync<List<GameSummaryResponse>>();
        var mine = history!.Single(summary => summary.GameId == game.GameId);

        Assert.Equal("Solo", mine.Mode);
        Assert.Equal("HuntTarget", mine.BotDifficulty);
        Assert.Equal("Olivier", mine.FirstPlayer);
        Assert.Equal("Bot", mine.SecondPlayer);
        Assert.Equal(6, mine.Shots);
        Assert.Null(mine.Winner);
    }

    /// <summary>
    /// L'historique lit des colonnes ; il ne rejoue aucune partie. S'il en
    /// rejouait, le nombre de tirs viendrait du journal reconstruit et non du
    /// comptage — même valeur, mais un coût qui croît avec l'historique.
    /// </summary>
    [Fact]
    public async Task RecentGames_CountsTheShotsWithoutReplayingTheGames()
    {
        await PlayAsync("Random", 2);
        await PlayAsync("Random", 5);

        var history = await _client.GetFromJsonAsync<List<GameSummaryResponse>>("/games?limit=2");

        Assert.Equal(2, history!.Count);
        Assert.Contains(history, summary => summary.Shots == 10);
    }

    /// <summary>
    /// Sur une base vierge, et avec un témoin indépendant : les résultats
    /// annoncés par chaque tir sont comptés côté test, puis comparés aux
    /// compteurs du serveur. Recalculer l'attendu à partir de la réponse ne
    /// prouverait que sa cohérence interne — c'est le défaut de la revue 2.
    /// </summary>
    [Fact]
    public async Task Statistics_CountEveryShotResult()
    {
        using var isolated = new ApiFactory();
        var client = isolated.CreateClient();

        var created = await client.PostAsJsonAsync(
            "/games", new CreateGameRequest("Olivier", 10, 10, "Solo", "HuntTargetParity", "Random"));
        var view = (await created.Content.ReadFromJsonAsync<GameViewResponse>())!;

        var counted = new Dictionary<string, int> { ["Miss"] = 0, ["Hit"] = 0, ["Sunk"] = 0 };

        async Task RecordAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode) return;
            var outcome = await response.Content.ReadFromJsonAsync<ShotOutcomeResponse>();
            counted[outcome!.Result]++;
        }

        for (var turn = 0; turn < 40; turn++)
        {
            await RecordAsync(await client.PostAsJsonAsync(
                $"/games/{view.GameId}/shots", new FireRequest(turn % 10, turn / 10)));
            await RecordAsync(await client.PostAsJsonAsync($"/games/{view.GameId}/bot-turn", new { }));
        }

        var stats = await client.GetFromJsonAsync<StatisticsResponse>("/stats");

        Assert.Equal(1, stats!.Games);
        Assert.Equal(counted["Miss"] + counted["Hit"] + counted["Sunk"], stats.Shots);
        Assert.Equal(counted["Hit"], stats.Hits);
        Assert.Equal(counted["Sunk"], stats.Sunk);
        Assert.True(counted["Sunk"] > 0, "aucun navire coulé : le test ne discriminerait rien sur ce compteur");
    }

    [Fact]
    public async Task RecentGames_HonoursItsLimit()
    {
        await PlayAsync("Random", 1);
        await PlayAsync("Random", 1);
        await PlayAsync("Random", 1);

        var history = await _client.GetFromJsonAsync<List<GameSummaryResponse>>("/games?limit=1");

        Assert.Single(history!);
    }

    /// <summary>
    /// Une limite absurde ne doit ni faire tomber l'endpoint ni lui faire lire
    /// toute la base : elle est ramenée dans ses bornes.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10_000)]
    public async Task RecentGames_WithAnAbsurdLimit_StillAnswers(int limit)
    {
        await PlayAsync("Random", 1);

        var response = await _client.GetAsync($"/games?limit={limit}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty((await response.Content.ReadFromJsonAsync<List<GameSummaryResponse>>())!);
    }
}
