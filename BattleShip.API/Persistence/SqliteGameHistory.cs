using BattleShip.Domain;
using Microsoft.EntityFrameworkCore;

namespace BattleShip.API.Persistence;

/// <summary>
/// Interroge des colonnes, ne reconstruit aucune partie : l'historique d'une
/// centaine de parties ne doit pas rejouer une centaine de journaux.
/// </summary>
public sealed class SqliteGameHistory(BattleShipDbContext db) : IGameHistory
{
    public IReadOnlyList<GameSummary> Recent(int limit) =>
    [
        .. db.Games
            .AsNoTracking()
            .OrderByDescending(game => game.StartedAt)
            .Take(limit)
            .Select(game => new
            {
                game.Id,
                game.Mode,
                game.BotDifficulty,
                game.WinnerName,
                game.StartedAt,
                game.FinishedAt,
                Shots = game.Shots.Count,
                First = game.Players.Where(player => player.Seat == 0).Select(player => player.Name).FirstOrDefault(),
                Second = game.Players.Where(player => player.Seat == 1).Select(player => player.Name).FirstOrDefault()
            })
            .AsEnumerable()
            .Select(game => new GameSummary(
                game.Id,
                EnumNames<GameMode>.Parse(game.Mode),
                EnumNames<BotDifficulty>.Parse(game.BotDifficulty),
                game.FinishedAt is null ? GameStatus.InProgress : GameStatus.Finished,
                game.First ?? string.Empty,
                game.Second ?? string.Empty,
                game.WinnerName,
                game.Shots,
                game.StartedAt,
                game.FinishedAt))
    ];

    public Statistics Overall() => new(
        db.Games.Count(),
        db.Games.Count(game => game.FinishedAt != null),
        db.Shots.Count(),
        db.Shots.Count(shot => shot.Result == nameof(ShotResult.Hit)),
        db.Shots.Count(shot => shot.Result == nameof(ShotResult.Sunk)));
}
