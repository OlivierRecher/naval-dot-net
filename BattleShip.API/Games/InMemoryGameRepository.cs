using System.Collections.Concurrent;
using BattleShip.Domain;

namespace BattleShip.API.Games;

/// <summary>
/// Enregistre en Singleton : deux requetes HTTP concurrentes partagent la meme
/// instance, d'ou le dictionnaire concurrent. Voir ADR 0004.
/// </summary>
public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<Guid, Game> _games = new();

    public void Add(Game game) => _games[game.Id] = game;

    public Game? Find(Guid id) => _games.TryGetValue(id, out var game) ? game : null;
}
