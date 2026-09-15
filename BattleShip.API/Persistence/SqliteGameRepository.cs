using System.Collections.Concurrent;
using BattleShip.Domain;
using Microsoft.EntityFrameworkCore;

namespace BattleShip.API.Persistence;

/// <summary>
/// SQLite détient le journal, la mémoire détient la partie en cours.
///
/// <see cref="Game"/> porte son propre verrou (ADR 0008), et ce verrou ne
/// sérialise quoi que ce soit que si les requêtes concurrentes obtiennent la
/// <b>même instance</b>. C'est <see cref="GameCache.Remember"/> qui le garantit :
/// il publie une instance ou rend celle qui a gagné la course. Le raccourci de
/// <see cref="Find"/> ne fait qu'éviter une lecture SQL — retiré, le
/// comportement est identique, seulement plus coûteux. Voir ADR 0012.
/// </summary>
public sealed class SqliteGameRepository(BattleShipDbContext db, GameCache cache) : IGameRepository
{
    public void Add(Game game)
    {
        var view = game.ViewFor(game.CurrentPlayer);

        var record = new GameRecord
        {
            Id = game.Id,
            Mode = game.Mode.ToString(),
            BotDifficulty = game.BotDifficulty.ToString(),
            Columns = view.Size.Columns,
            Rows = view.Size.Rows,
            StartedAt = DateTimeOffset.UtcNow,
            Players =
            [
                PlayerOf(game.CurrentPlayer, game.Id, seat: 0),
                PlayerOf(game.Opponent, game.Id, seat: 1)
            ]
        };

        db.Games.Add(record);
        db.SaveChanges();

        cache.Remember(game);
    }

    /// <summary>
    /// Le raccourci par le cache est une <b>optimisation</b> : sans lui, chaque
    /// lecture interroge SQLite puis jette la partie reconstruite au profit de
    /// celle deja publiee. L'identite tient a Remember, pas a ce test.
    /// </summary>
    public Game? Find(Guid id) => cache.Remembered(id) ?? Load(id);

    public void Save(Game game)
    {
        var record = db.Games
            .Include(entity => entity.Players)
            .ThenInclude(player => player.Ships)
            .SingleOrDefault(entity => entity.Id == game.Id);

        if (record is null)
        {
            return;
        }

        record.WinnerName = game.Winner?.Name;

        if (game.Status is GameStatus.Finished && record.FinishedAt is null)
        {
            record.FinishedAt = DateTimeOffset.UtcNow;
        }

        SyncFleets(record, game);
        AppendShots(record.Id, game);

        db.SaveChanges();
    }

    /// <summary>
    /// Les flottes n'existent pas forcément à la création : en placement manuel
    /// elles arrivent plus tard. Elles ne changent jamais ensuite, donc on ne
    /// pose que celles qui manquent.
    /// </summary>
    private void SyncFleets(GameRecord record, Game game)
    {
        foreach (var (player, board) in Boards(game))
        {
            var stored = record.Players.SingleOrDefault(entity => entity.Id == player.Id);

            if (stored is null || stored.Ships.Count > 0 || board.Ships.Count is 0)
            {
                continue;
            }

            stored.Ships.AddRange(board.Ships.Select(ship => new ShipRecord
            {
                PlayerId = player.Id,
                Kind = ship.Kind.ToString(),
                Column = ship.Placement.Origin.Column,
                Row = ship.Placement.Origin.Row,
                Orientation = ship.Placement.Orientation.ToString()
            }));
        }
    }

    private void AppendShots(Guid gameId, Game game)
    {
        var alreadyStored = db.Shots.Count(shot => shot.GameId == gameId);
        var journal = game.Shots;

        for (var ordinal = alreadyStored; ordinal < journal.Count; ordinal++)
        {
            var shot = journal[ordinal];

            db.Shots.Add(new ShotRecord
            {
                GameId = gameId,
                Ordinal = ordinal,
                ShooterId = shot.ShooterId,
                Column = shot.Target.Column,
                Row = shot.Target.Row,
                Result = shot.Result.ToString()
            });
        }
    }

    private Game? Load(Guid id)
    {
        var record = db.Games
            .AsNoTracking()
            .Include(entity => entity.Players)
            .ThenInclude(player => player.Ships)
            .Include(entity => entity.Shots)
            .SingleOrDefault(entity => entity.Id == id);

        if (record is null)
        {
            return null;
        }

        var size = new BoardSize(record.Columns, record.Rows);
        var seats = record.Players.OrderBy(player => player.Seat).ToList();

        var game = Game.Restore(
            record.Id,
            EnumNames<GameMode>.Parse(record.Mode),
            PlayerFrom(seats[0], size),
            PlayerFrom(seats[1], size),
            EnumNames<BotDifficulty>.Parse(record.BotDifficulty),
            [.. record.Shots.OrderBy(shot => shot.Ordinal).Select(shot => new Coordinates(shot.Column, shot.Row))]);

        return cache.Remember(game);
    }

    private static IEnumerable<(Player Player, Board Board)> Boards(Game game) =>
        [(game.CurrentPlayer, game.CurrentPlayer.Board), (game.Opponent, game.Opponent.Board)];

    private static PlayerRecord PlayerOf(Player player, Guid gameId, int seat) => new()
    {
        Id = player.Id,
        GameId = gameId,
        Seat = seat,
        Name = player.Name,
        IsBot = player.IsBot,
        Ships =
        [
            .. player.Board.Ships.Select(ship => new ShipRecord
            {
                PlayerId = player.Id,
                Kind = ship.Kind.ToString(),
                Column = ship.Placement.Origin.Column,
                Row = ship.Placement.Origin.Row,
                Orientation = ship.Placement.Orientation.ToString()
            })
        ]
    };

    private static Player PlayerFrom(PlayerRecord record, BoardSize size)
    {
        var board = new Board(size);

        foreach (var ship in record.Ships)
        {
            board.Place(new ShipPlacement(
                EnumNames<ShipKind>.Parse(ship.Kind),
                new Coordinates(ship.Column, ship.Row),
                EnumNames<Orientation>.Parse(ship.Orientation)));
        }

        return new Player(record.Name, record.IsBot, board, record.Id);
    }
}

/// <summary>
/// Les parties vivantes, partagées par toutes les requêtes du processus. Voir
/// <see cref="SqliteGameRepository"/> pour la raison — le verrou de l'agrégat.
/// </summary>
public sealed class GameCache
{
    private readonly ConcurrentDictionary<Guid, Game> _games = new();

    public Game Remember(Game game) => _games.GetOrAdd(game.Id, game);

    public Game? Remembered(Guid id) => _games.GetValueOrDefault(id);
}
