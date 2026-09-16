using System.Net;
using System.Net.Http.Json;
using BattleShip.Models;

namespace BattleShip.Tests.Api;

public class HistoryEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>
    /// Joue <paramref name="rounds"/> allers-retours et rend le nombre de tirs
    /// réellement acceptés. Il n'est plus déductible du nombre de tours : le bot
    /// enchaîne tant qu'il touche. Ce compte est tenu par le test, à partir des
    /// réponses de chaque tir — c'est le témoin indépendant du comptage SQL.
    /// </summary>
    private async Task<(GameViewResponse View, int Shots)> PlayAsync(string difficulty, int rounds)
    {
        var created = await _client.PostAsJsonAsync(
            "/games", new CreateGameRequest("Olivier", 10, 10, "Solo", difficulty, "Random"));
        var view = (await created.Content.ReadFromJsonAsync<GameViewResponse>())!;

        var shots = 0;

        for (var round = 0; round < rounds; round++)
        {
            var played = await _client.PlayRoundAsync(view.GameId, round % 10, round / 10);
            shots += played.Count;

            if (played[^1].GameOver)
            {
                break;
            }
        }

        return (view, shots);
    }

    [Fact]
    public async Task RecentGames_ListsWhatWasPlayed()
    {
        var (game, shots) = await PlayAsync("HuntTarget", 3);

        var response = await _client.GetAsync("/games");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await response.Content.ReadFromJsonAsync<List<GameSummaryResponse>>();
        var mine = history!.Single(summary => summary.GameId == game.GameId);

        Assert.Equal("Solo", mine.Mode);
        Assert.Equal("HuntTarget", mine.BotDifficulty);
        Assert.Equal("Olivier", mine.FirstPlayer);
        Assert.Equal("Bot", mine.SecondPlayer);
        Assert.Equal(shots, mine.Shots);
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
        var (longest, shots) = await PlayAsync("Random", 5);

        var history = await _client.GetFromJsonAsync<List<GameSummaryResponse>>("/games?limit=2");

        Assert.Equal(2, history!.Count);
        Assert.Equal(shots, history.Single(summary => summary.GameId == longest.GameId).Shots);
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

        for (var round = 0; round < 40; round++)
        {
            var played = await client.PlayRoundAsync(view.GameId, round % 10, round / 10);

            foreach (var outcome in played)
            {
                counted[outcome.Result]++;
            }

            if (played[^1].GameOver)
            {
                break;
            }
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

    /// <summary>
    /// Remarque de la revue de la PR #7. Une partie en placement manuel n'est ni
    /// terminée ni en cours : l'historique l'annonçait « InProgress » tant que
    /// les flottes n'étaient pas posées. Le statut se déduit des flottes, aucune
    /// colonne n'a été ajoutée pour cela.
    /// </summary>
    [Fact]
    public async Task RecentGames_ReportAGameStillWaitingForItsFleet()
    {
        using var isolated = new ApiFactory();
        var client = isolated.CreateClient();

        var created = await client.PostAsJsonAsync(
            "/games", new CreateGameRequest("Olivier", 10, 10, "Solo", "Random", "Manual"));
        var view = (await created.Content.ReadFromJsonAsync<GameViewResponse>())!;

        var history = await client.GetFromJsonAsync<List<GameSummaryResponse>>("/games");

        Assert.Equal("AwaitingFleet", history!.Single(summary => summary.GameId == view.GameId).Status);

        await client.PutAsJsonAsync($"/games/{view.GameId}/fleet", new PlaceFleetRequest(
        [
            new("Carrier", 0, 0, "Vertical"),
            new("Battleship", 2, 0, "Vertical"),
            new("Cruiser", 4, 0, "Vertical"),
            new("Submarine", 6, 0, "Vertical"),
            new("Destroyer", 8, 0, "Vertical")
        ]));

        history = await client.GetFromJsonAsync<List<GameSummaryResponse>>("/games");

        Assert.Equal("InProgress", history!.Single(summary => summary.GameId == view.GameId).Status);
    }
}
