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

    // L'ordre d'ouverture ne change jamais, contrairement a _current/_waiting qui
    // s'echangent a chaque tour. La persistance en depend : c'est lui qui dit
    // quel joueur rejouer en premier. Voir ADR 0012.
    private readonly Player _first;
    private readonly Player _second;

    private Player _current;
    private Player _waiting;

    /// <summary>
    /// Le niveau du bot est une donnee de la partie, comme le mode : il est fixe
    /// a la creation et ne change plus. Il est optionnel parce qu'une partie
    /// <see cref="GameMode.Local"/> n'oppose aucun bot — exiger un niveau y
    /// reviendrait a inventer une donnee sans objet.
    /// </summary>
    public Game(
        GameMode mode,
        Player first,
        Player second,
        BotDifficulty botDifficulty = BotDifficulty.Random,
        IReadOnlyList<ShipKind>? fleet = null)
    {
        Mode = mode;
        BotDifficulty = botDifficulty;
        Fleet = fleet ?? FleetTemplate.Standard;
        _first = first;
        _second = second;
        _current = first;
        _waiting = second;

        // Le statut se deduit des grilles plutot que d'un drapeau : un drapeau
        // pourrait contredire l'etat reel des flottes.
        Status = first.Board.Ships.Count is 0 || second.Board.Ships.Count is 0
            ? GameStatus.AwaitingFleet
            : GameStatus.InProgress;
    }

    public Guid Id { get; private init; } = Guid.NewGuid();

    public GameMode Mode { get; }

    public BotDifficulty BotDifficulty { get; }

    /// <summary>
    /// La composition en jeu, identique pour les deux joueurs. Elle etait en dur
    /// jusqu'a l'item 6 ; l'ADR 0010 l'avait note comme dette. Voir ADR 0013.
    /// </summary>
    public IReadOnlyList<ShipKind> Fleet { get; }

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

    /// <summary>
    /// Reconstruit une partie a partir de ce qui a ete persiste : les placements,
    /// deja poses sur les grilles recues, et le journal ordonne. Tout le reste —
    /// cases touchees, navires coules, tour courant, statut, vainqueur — est
    /// rejoue, jamais stocke. C'est ce que l'ADR 0002 annonçait ; l'ADR 0012 en
    /// fait la strategie de persistance.
    /// </summary>
    public static Game Restore(
        Guid id,
        GameMode mode,
        Player first,
        Player second,
        BotDifficulty botDifficulty,
        IReadOnlyList<Coordinates> shots,
        IReadOnlyList<ShipKind>? fleet = null)
    {
        var game = new Game(mode, first, second, botDifficulty, fleet) { Id = id };

        foreach (var target in shots)
        {
            if (!game.Fire(target).IsAccepted)
            {
                throw new InvalidOperationException(
                    $"Journal incoherent : le tir en {target} est refuse au rejeu de la partie {id}.");
            }
        }

        return game;
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

            if (FleetPlacementRules.Validate(board.Size, Fleet, placements) is { } rejection)
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

    /// <summary>
    /// Une photographie coherente de la partie, prise sous le verrou. Sans elle,
    /// un lecteur externe lirait <c>Status</c>, les joueurs et les grilles a des
    /// instants differents : l'echange de tour est une affectation de tuple, donc
    /// non atomique, et <c>Board.Ships</c> est une liste vivante. Voir ADR 0012.
    /// </summary>
    public GameState Snapshot()
    {
        lock (_gate)
        {
            return new GameState(
                Id,
                Mode,
                BotDifficulty,
                Status,
                _first.Board.Size,
                Fleet,
                Winner?.Name,
                StateOf(_first),
                StateOf(_second),
                [.. _shots]);
        }
    }

    private IReadOnlyList<ShipToPlace> FleetAsShipsToPlace =>
        [.. Fleet.Select(kind => new ShipToPlace(kind, kind.Size()))];

    private static PlayerState StateOf(Player player) => new(
        player.Id,
        player.Name,
        player.IsBot,
        [.. player.Board.Ships.Select(ship => ship.Placement)]);

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
                ? FleetAsShipsToPlace
                : [],
            FleetAsShipsToPlace,
            Winner?.Name);
    }
}
