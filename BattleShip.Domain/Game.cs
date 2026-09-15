namespace BattleShip.Domain;

public enum GameMode
{
    Solo,
    Local
}

public enum GameStatus
{
    AwaitingFleet,
    InProgress,
    Finished
}

public sealed class Game
{
    private readonly Lock _gate = new();
    private readonly List<Shot> _shots = [];
    private Player _current;
    private Player _waiting;

    /// <summary>
    /// Le niveau du bot est une donnee de la partie, comme le mode : il est fixe
    /// a la creation et ne change plus. Il est optionnel parce qu'une partie
    /// <see cref="GameMode.Local"/> n'oppose aucun bot — exiger un niveau y
    /// reviendrait a inventer une donnee sans objet.
    /// </summary>
    public Game(GameMode mode, Player first, Player second, BotDifficulty botDifficulty = BotDifficulty.Random)
    {
        Mode = mode;
        BotDifficulty = botDifficulty;
        _current = first;
        _waiting = second;

        // Le statut se deduit des grilles plutot que d'un drapeau : un drapeau
        // pourrait contredire l'etat reel des flottes.
        Status = first.Board.Ships.Count is 0 || second.Board.Ships.Count is 0
            ? GameStatus.AwaitingFleet
            : GameStatus.InProgress;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public GameMode Mode { get; }

    public BotDifficulty BotDifficulty { get; }

    public GameStatus Status { get; private set; }

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
            // Ce garde n'est pas redondant avec FireRules : sans lui, le refus
            // deviendrait NotTheBotTurn, qui decrit mal la situation.
            if (Status is GameStatus.AwaitingFleet)
            {
                return FireOutcome.Rejected(FireRejection.FleetNotPlaced);
            }

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
    /// Pose la flotte demandee par le navigateur. Le client n'envoie aucune
    /// identite : le serveur remplit la grille du seul humain qui en attend une,
    /// donc jamais celle d'un bot. Voir ADR 0003.
    /// </summary>
    public FleetOutcome PlaceFleetFromClient(IReadOnlyList<ShipPlacement> placements)
    {
        lock (_gate)
        {
            if (PlayerAwaitingFleet() is not { } placer)
            {
                return FleetOutcome.Rejected(FleetRejection.FleetAlreadyPlaced);
            }

            var board = placer.Board;

            if (FleetPlacementRules.Validate(board.Size, FleetTemplate.Standard, placements) is { } rejection)
            {
                return FleetOutcome.Rejected(rejection);
            }

            foreach (var placement in placements)
            {
                board.Place(placement);
            }

            if (PlayerAwaitingFleet() is null)
            {
                Status = GameStatus.InProgress;
            }

            return FleetOutcome.Accepted;
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
            // Pendant le placement, le viewer n'est pas le joueur courant mais
            // celui dont on attend la flotte : c'est lui qui est devant l'ecran.
            // Sans cela, en mode Local, le second joueur recevrait une vue du
            // premier, sans rien a poser et sans moyen d'avancer.
            if (Status is GameStatus.AwaitingFleet && PlayerAwaitingFleet() is { } placer)
            {
                return ViewLocked(placer);
            }

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

    /// <summary>
    /// Le prochain humain dont on attend la flotte. Un bot n'y figure jamais :
    /// le serveur remplit sa grille lui-meme, donc le client ne peut pas la
    /// poser a sa place. Voir ADR 0003.
    /// </summary>
    private Player? PlayerAwaitingFleet() =>
        new[] { _current, _waiting }
            .FirstOrDefault(player => !player.IsBot && player.Board.Ships.Count is 0);

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
            // Un nom, jamais une position : l'adversaire est deja connu du
            // joueur, qui l'a saisi ou affronte un bot. Voir ADR 0003.
            ReferenceEquals(viewer, _current) ? _waiting.Name : _current.Name,
            ReferenceEquals(viewer, _current),
            [.. viewer.Board.Ships.Select(ship => new ShipView(ship.Kind, ship.Cells, [.. ship.Hits], ship.IsSunk))],
            [.. viewer.Board.IncomingShots],
            [.. _shots.Where(shot => shot.ShooterId == viewer.Id)
                      .Select(shot => new RevealedCell(shot.Target, shot.Result))],
            BotDifficulty,
            Status is GameStatus.AwaitingFleet && viewer.Board.Ships.Count is 0
                ? [.. FleetTemplate.Standard.Select(kind => new ShipToPlace(kind, kind.Size()))]
                : [],
            Winner?.Name);
    }
}
