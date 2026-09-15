using System.Net.Http.Json;
using BattleShip.Grpc;
using BattleShip.Models;
using Grpc.Core;

namespace BattleShip.App.Services;

/// <summary>
/// Detient l'etat de la partie et concentre TOUS les appels reseau : aucun
/// composant n'appelle HttpClient ni le client gRPC directement.
/// Voir AGENTS.md § 6.
/// </summary>
public sealed class GameSession(HttpClient http, Battle.BattleClient battle)
{
    public GameViewResponse? View { get; private set; }

    public string? Notice { get; private set; }

    public string? Failure { get; private set; }

    public bool IsBusy { get; private set; }

    public event Action? OnChange;

    public bool IsOver => View?.Status == nameof(GameStatusNames.Finished);

    public bool IsPlacingFleet => View?.Status == nameof(GameStatusNames.AwaitingFleet);

    public bool CanFire => View is not null && !IsOver && !IsPlacingFleet && View.IsViewerTurn && !IsBusy;

    public bool CanRetryBotTurn => View is not null && !IsOver && !IsPlacingFleet && !View.IsViewerTurn && !IsBusy;

    public async Task StartAsync(string playerName, int side, string botDifficulty, string fleetPlacement)
    {
        await RunAsync(async () =>
        {
            var response = await http.PostAsJsonAsync("games", new CreateGameRequest(playerName, side, side, botDifficulty, fleetPlacement));

            if (!response.IsSuccessStatusCode)
            {
                Failure = $"Création refusée par le serveur (HTTP {(int)response.StatusCode}).";
                return;
            }

            View = await response.Content.ReadFromJsonAsync<GameViewResponse>();

            Notice = IsPlacingFleet
                ? $"Partie créée contre le bot {BotDifficultyCatalog.LabelOf(View?.BotDifficulty)}. Posez votre flotte."
                : $"Partie créée contre le bot {BotDifficultyCatalog.LabelOf(View?.BotDifficulty)}. À vous de jouer.";
        });
    }

    /// <summary>
    /// Le tir passe par gRPC-Web ; le reste du contrat reste en HTTP/JSON.
    /// Voir ADR 0005.
    /// </summary>
    public async Task FireAsync(int column, int row)
    {
        if (View is null)
        {
            return;
        }

        var gameId = View.GameId;

        await RunAsync(async () =>
        {
            try
            {
                var outcome = await battle.FireAsync(new FireCommand
                {
                    GameId = gameId.ToString(),
                    Column = column,
                    Row = row
                });

                Notice = Describe(outcome.Result, "Vous");

                if (outcome.GameOver)
                {
                    await RefreshAsync(gameId);
                    return;
                }
            }
            catch (RpcException error) when (error.StatusCode is StatusCode.FailedPrecondition or StatusCode.InvalidArgument)
            {
                // Tir refuse : le tour n'est pas consomme, l'etat n'a pas bouge.
                Notice = error.Status.Detail;
                return;
            }

            await PlayBotTurnAsync(gameId);
            await RefreshAsync(gameId);
        });
    }

    /// <summary>
    /// Rejoue le tour du bot apres un echec reseau : sans cela la partie reste
    /// bloquee sur « Le bot joue… » et le joueur doit l'abandonner.
    /// </summary>
    public async Task RetryBotTurnAsync()
    {
        if (View is null || IsOver)
        {
            return;
        }

        var gameId = View.GameId;

        await RunAsync(async () =>
        {
            await PlayBotTurnAsync(gameId);
            await RefreshAsync(gameId);
        });
    }

    /// <summary>
    /// Pose la flotte. Le navigateur verifie deja le debordement et le
    /// chevauchement pour eviter un aller-retour par navire, mais c'est le
    /// serveur qui refuse : le controle local est un confort, pas la garantie.
    /// </summary>
    public async Task PlaceFleetAsync(IReadOnlyList<ShipPlacementDto> ships)
    {
        if (View is null)
        {
            return;
        }

        var gameId = View.GameId;

        await RunAsync(async () =>
        {
            var response = await http.PutAsJsonAsync($"games/{gameId}/fleet", new PlaceFleetRequest(ships));

            if (!response.IsSuccessStatusCode)
            {
                Failure = $"Placement refusé par le serveur (HTTP {(int)response.StatusCode}). Votre flotte n'est pas posée.";
                return;
            }

            View = await response.Content.ReadFromJsonAsync<GameViewResponse>();
            Notice = "Flotte en place. À vous de jouer.";
        });
    }

    /// <summary>Revient a l'ecran de creation sans toucher a la partie cote serveur.</summary>
    public void Forget()
    {
        View = null;
        Notice = null;
        Failure = null;
        OnChange?.Invoke();
    }

    private async Task PlayBotTurnAsync(Guid gameId)
    {
        var response = await http.PostAsJsonAsync($"games/{gameId}/bot-turn", new { });

        if (!response.IsSuccessStatusCode)
        {
            Failure = $"Le bot n'a pas pu jouer (HTTP {(int)response.StatusCode}). Votre tir est enregistré ; relancez le tour du bot.";
            return;
        }

        var outcome = await response.Content.ReadFromJsonAsync<ShotOutcomeResponse>();

        if (outcome is not null)
        {
            var cell = $"{(char)('A' + outcome.Target.Column)}{outcome.Target.Row + 1}";
            Notice += $" — Le bot tire en {cell} : {Describe(outcome.Result, "il")}";
        }
    }

    private async Task RefreshAsync(Guid gameId) =>
        View = await http.GetFromJsonAsync<GameViewResponse>($"games/{gameId}");

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        Failure = null;
        OnChange?.Invoke();

        try
        {
            await action();
        }
        catch (HttpRequestException)
        {
            Failure = "Le serveur est injoignable. Vérifiez que l'API est démarrée, puis réessayez.";
        }
        catch (RpcException)
        {
            Failure = "L'échange gRPC a échoué. Vérifiez que l'API est démarrée, puis réessayez.";
        }
        finally
        {
            IsBusy = false;
            OnChange?.Invoke();
        }
    }

    private static string Describe(string result, string subject) => result switch
    {
        "Miss" => $"{subject} manque.",
        "Hit" => $"{subject} touche !",
        "Sunk" => $"{subject} coule un navire !",
        _ => result
    };

    private enum GameStatusNames
    {
        AwaitingFleet,
        InProgress,
        Finished
    }
}
