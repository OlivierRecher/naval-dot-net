namespace BattleShip.Domain;

public enum GameMode
{
    Solo,
    Local
}

public enum GameStatus
{
    InProgress,
    Finished
}

public sealed class Game
{
    private readonly List<Shot> _shots = [];
    private Player _current;
    private Player _waiting;

    public Game(GameMode mode, Player first, Player second)
    {
        Mode = mode;
        _current = first;
        _waiting = second;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public GameMode Mode { get; }

    public GameStatus Status { get; private set; } = GameStatus.InProgress;

    public Player CurrentPlayer => _current;

    public Player Opponent => _waiting;

    public Player? Winner { get; private set; }

    public IReadOnlyList<Shot> Shots => _shots;

    public FireOutcome Fire(Coordinates target)
    {
        if (FireRules.Validate(Status, _waiting.Board, target) is { } rejection)
        {
            return FireOutcome.Rejected(rejection);
        }

        var shooter = _current;
        var result = _waiting.Board.Receive(target);
        _shots.Add(new Shot(shooter.Id, target, result));

        if (_waiting.Board.AllShipsSunk)
        {
            Status = GameStatus.Finished;
            Winner = shooter;
            return FireOutcome.Accepted(result);
        }

        (_current, _waiting) = (_waiting, _current);

        return FireOutcome.Accepted(result);
    }

    /// <summary>
    /// Projection destinee au navigateur. Un bot n'a pas de client : lui servir
    /// sa propre vue reviendrait a publier sa flotte. Voir ADR 0003.
    /// </summary>
    public GameView ViewForClient() => ViewFor(_current.IsBot ? _waiting : _current);

    public GameView ViewFor(Player viewer)
    {
        return new GameView(
            Id,
            Status,
            Mode,
            viewer.Board.Size,
            viewer.Name,
            ReferenceEquals(viewer, _current),
            [.. viewer.Board.Ships.Select(ship => new ShipView(ship.Kind, ship.Cells, [.. ship.Hits], ship.IsSunk))],
            [.. viewer.Board.IncomingShots],
            [.. _shots.Where(shot => shot.ShooterId == viewer.Id)
                      .Select(shot => new RevealedCell(shot.Target, shot.Result))],
            Winner?.Name);
    }
}
