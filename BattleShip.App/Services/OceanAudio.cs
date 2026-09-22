using Microsoft.JSInterop;

namespace BattleShip.App.Services;

/// <summary>
/// Habille la partie de sons synthetises dans le navigateur. Comme GameSession
/// pour le reseau, c'est le seul endroit qui parle a JavaScript : aucun
/// composant n'appelle IJSRuntime directement.
/// </summary>
public sealed class OceanAudio(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private bool _unavailable;

    public bool IsMuted { get; private set; }

    public async Task PrimeAsync()
    {
        var module = await ModuleAsync();

        if (module is null)
        {
            return;
        }

        try
        {
            var state = await module.InvokeAsync<AudioState>("prime");
            IsMuted = state.Muted;
            _unavailable = !state.Available;
        }
        catch (JSException)
        {
            _unavailable = true;
        }
    }

    /// <summary>
    /// Joue sans attendre : une voix qui tarde ne doit pas retenir le rendu de
    /// la grille, et un son perdu vaut mieux qu'un tir qui parait bloque.
    /// </summary>
    public void Play(string voice, double delaySeconds = 0) => _ = PlayAsync(voice, delaySeconds);

    public async Task<bool> ToggleMuteAsync()
    {
        var module = await ModuleAsync();

        if (module is null)
        {
            return IsMuted;
        }

        try
        {
            IsMuted = await module.InvokeAsync<bool>("setMuted", !IsMuted);
        }
        catch (JSException)
        {
            _unavailable = true;
        }

        return IsMuted;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("stop");
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // L'onglet part avant nous : il n'y a plus de module a liberer.
        }
        catch (JSException)
        {
        }

        _module = null;
    }

    private async Task PlayAsync(string voice, double delaySeconds)
    {
        var module = await ModuleAsync();

        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("play", voice, delaySeconds);
        }
        catch (JSException)
        {
            _unavailable = true;
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// Le module est charge une fois, a la demande. Un echec est definitif :
    /// reessayer a chaque tir ferait une requete perdue par case cliquee.
    /// </summary>
    private async Task<IJSObjectReference?> ModuleAsync()
    {
        if (_unavailable)
        {
            return null;
        }

        try
        {
            return _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/ocean.js");
        }
        catch (JSException)
        {
            _unavailable = true;
            return null;
        }
        catch (JSDisconnectedException)
        {
            _unavailable = true;
            return null;
        }
    }

    private sealed record AudioState(bool Available, bool Muted);
}
