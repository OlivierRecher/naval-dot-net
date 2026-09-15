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

    public IReadOnlyList<GameSummaryResponse> History { get; private set; } = [];

    public StatisticsResponse? Statistics { get; private set; }

    public bool IsOver => View?.Status == nameof(GameStatusNames.Finished);

    public bool IsPlacingFleet => View?.Status == nameof(GameStatusNames.AwaitingFleet);

    public bool IsHotSeat => View?.Mode == nameof(GameModeNames.Local);

    /// <summary>
    /// Nom du joueur a qui l'appareil doit etre passe, ou null. En hot-seat, la
    /// vue suivante decrit deja l'autre joueur — donc sa flotte : tant que cette
    /// propriete n'est pas nulle, l'interface ne doit rien afficher de la vue.
    /// C'est une protection d'affichage, pas une protection reseau : la vue est
    /// deja dans le navigateur. Voir AGENTS.md § 4 et l'ADR 0011.
    /// </summary>
    public string? HandoverTo { get; private set; }

    public bool IsAwaitingHandover => HandoverTo is not null;

    /// <summary>
    /// La passation est armee des que le tour a change cote serveur, donc avant
    /// d'avoir obtenu la vue suivante. Tant que celle-ci n'est pas arrivee, il
    /// n'y a rien a confirmer : confirmer afficherait la vue perimee du joueur
    /// precedent — c'est-a-dire SA flotte.
    /// </summary>
    public bool IsHandoverReady => HandoverTo is not null && View?.ViewerName == HandoverTo;

    public bool CanFire => View is not null && !IsOver && !IsPlacingFleet && !IsAwaitingHandover && View.IsViewerTurn && !IsBusy;

    public bool CanRetryBotTurn => View is not null && !IsOver && !IsPlacingFleet && !IsHotSeat && !View.IsViewerTurn && !IsBusy;

    public async Task StartAsync(
        string playerName,
        int side,
        string mode,
        string botDifficulty,
        string fleetPlacement,
        string? opponentName,
        IReadOnlyList<string>? fleet)
    {
        await RunAsync(async () =>
        {
            var response = await http.PostAsJsonAsync("games", new CreateGameRequest(playerName, side, side, mode, botDifficulty, fleetPlacement, opponentName, fleet));

            if (!response.IsSuccessStatusCode)
            {
                Failure = $"Création refusée par le serveur (HTTP {(int)response.StatusCode}).";
                return;
            }

            View = await response.Content.ReadFromJsonAsync<GameViewResponse>();

            var opponent = IsHotSeat
                ? "en duel"
                : $"contre le bot {BotDifficultyCatalog.LabelOf(View?.BotDifficulty)}";

            Notice = IsPlacingFleet
                ? $"Partie créée {opponent}. {View?.ViewerName} pose sa flotte."
                : $"Partie créée {opponent}. À {View?.ViewerName} de jouer.";
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

        // En hot-seat, la vue bascule sur l'autre joueur des le tir resolu : le
        // nom du tireur doit etre retenu avant, sinon le message s'adresse au
        // mauvais joueur. Celui du suivant aussi, pour armer la passation sans
        // dependre du rafraichissement.
        var shooter = IsHotSeat ? View.ViewerName : "Vous";
        var nextPlayer = IsHotSeat ? View.OpponentName : null;

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

                Notice = Describe(outcome.Result, shooter);

                if (outcome.GameOver)
                {
                    await RefreshAsync(gameId);
                    return;
                }

                if (nextPlayer is not null)
                {
                    // Arme AVANT le rafraichissement : le tour a deja change cote
                    // serveur. Si le GET echoue, l'interface reste sur l'ecran de
                    // passation au lieu de rendre la vue perimee du tireur — qui
                    // le laisserait rejouer, et le serveur accepterait ce tir
                    // comme celui de l'adversaire. La passation est le seul garde
                    // du tour en hot-seat.
                    HandoverTo = nextPlayer;
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
        var nextPlayer = IsHotSeat ? View.OpponentName : null;

        await RunAsync(async () =>
        {
            var response = await http.PutAsJsonAsync($"games/{gameId}/fleet", new PlaceFleetRequest(ships));

            if (!response.IsSuccessStatusCode)
            {
                Failure = $"Placement refusé par le serveur (HTTP {(int)response.StatusCode}). Votre flotte n'est pas posée.";
                return;
            }

            // Meme raison qu'au tir : la flotte est posee cote serveur, la
            // passation ne doit pas dependre de la lecture de la reponse.
            HandoverTo = nextPlayer;

            View = await response.Content.ReadFromJsonAsync<GameViewResponse>();

            if (IsPlacingFleet)
            {
                Notice = "Flotte enregistrée.";
                return;
            }

            HandoverTo = null;

            Notice = "Flotte en place. À vous de jouer.";
        });
    }

    /// <summary>
    /// L'historique passe par ce service comme le reste : AGENTS.md § 6 veut un
    /// seul chemin reseau, donc un seul endroit ou vivent « chargement, succes,
    /// echec ». Une page qui appellerait HttpClient elle-meme en creerait un
    /// second, avec sa propre gestion d'erreur.
    /// </summary>
    public async Task LoadHistoryAsync()
    {
        await RunAsync(async () =>
        {
            Statistics = await http.GetFromJsonAsync<StatisticsResponse>("stats");
            History = await http.GetFromJsonAsync<List<GameSummaryResponse>>("games?limit=20") ?? [];
        });
    }

    /// <summary>L'appareil a change de mains : la vue peut etre affichee.</summary>
    public void ConfirmHandover()
    {
        if (!IsHandoverReady)
        {
            return;
        }

        HandoverTo = null;
        OnChange?.Invoke();
    }

    /// <summary>Rejoue le rafraichissement quand il a echoue pendant la passation.</summary>
    public async Task RetryHandoverAsync()
    {
        if (View is null || HandoverTo is null)
        {
            return;
        }

        var gameId = View.GameId;

        await RunAsync(() => RefreshAsync(gameId));
    }

    /// <summary>Revient a l'ecran de creation sans toucher a la partie cote serveur.</summary>
    public void Forget()
    {
        View = null;
        Notice = null;
        Failure = null;
        HandoverTo = null;
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

    private enum GameModeNames
    {
        Solo,
        Local
    }

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
