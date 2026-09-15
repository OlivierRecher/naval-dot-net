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
    private readonly Lock _gate = new();
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

    public IReadOnlyList<Shot> Shots
    {
        get
        {
            lock (_gate)
            {
                return [.. _shots];
            }
        }
    }

    public FireOutcome Fire(Coordinates target)
    {
        lock (_gate)
        {
            return FireLocked(target);
        }
    }

    /// <summary>
    /// Tir demande par le navigateur. Le client n'envoie aucune identite : le
    /// serveur refuse donc de jouer le tour d'un bot a sa place, sinon le
    /// client choisirait la cible du bot. Voir ADR 0003.
    /// </summary>
    public FireOutcome FireFromClient(Coordinates target)
    {
        lock (_gate)
        {
            if (Status is GameStatus.Finished)
            {
                return FireOutcome.Rejected(FireRejection.GameFinished);
            }

            return _current.IsBot
                ? FireOutcome.Rejected(FireRejection.NotTheClientTurn)
                : FireLocked(target);
        }
    }

    /// <summary>
    /// Le choix de la cible et le tir forment une seule transition : les
    /// separer laisserait deux appels concurrents jouer deux fois le meme tour.
    /// </summary>
    public FireOutcome PlayBotTurn(IBotStrategy strategy)
    {
        lock (_gate)
        {
            if (Status is GameStatus.Finished)
            {
                return FireOutcome.Rejected(FireRejection.GameFinished);
            }

            if (!_current.IsBot)
            {
                return FireOutcome.Rejected(FireRejection.NotTheBotTurn);
            }

            return FireLocked(strategy.ChooseTarget(ViewLocked(_current), _waiting.Board.Size));
        }
    }

    /// <summary>
    /// Projection destinee au navigateur. Un bot n'a pas de client : lui servir
    /// sa propre vue reviendrait a publier sa flotte. Voir ADR 0003.
    /// </summary>
    public GameView ViewForClient()
    {
        lock (_gate)
        {
            return ViewLocked(_current.IsBot ? _waiting : _current);
        }
    }

    public GameView ViewFor(Player viewer)
    {
        lock (_gate)
        {
            return ViewLocked(viewer);
        }
    }

    private FireOutcome FireLocked(Coordinates target)
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
            return FireOutcome.Accepted(target, result);
        }

        (_current, _waiting) = (_waiting, _current);

        return FireOutcome.Accepted(target, result);
    }

    private GameView ViewLocked(Player viewer)
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
