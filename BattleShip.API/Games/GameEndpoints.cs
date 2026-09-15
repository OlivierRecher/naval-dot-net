using BattleShip.API.Validation;
using BattleShip.Domain;
using BattleShip.Models;

namespace BattleShip.API.Games;

public static class GameEndpoints
{
    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder routes)
    {
        var games = routes.MapGroup("/games").WithTags("Games");

        games.MapPost("/", CreateGame)
            .WithName("CreateGame")
            .WithValidation<CreateGameRequest>();

        games.MapGet("/{id:guid}", GetGame)
            .WithName("GetGame");

        games.MapPost("/{id:guid}/shots", Fire)
            .WithName("Fire")
            .WithValidation<FireRequest>();

        games.MapPost("/{id:guid}/bot-turn", PlayBotTurn)
            .WithName("PlayBotTurn");

        games.MapPut("/{id:guid}/fleet", PlaceFleet)
            .WithName("PlaceFleet")
            .WithValidation<PlaceFleetRequest>();

        return routes;
    }

    private static IResult CreateGame(
        CreateGameRequest request,
        IGameRepository repository,
        RandomFleetPlacer placer)
    {
        var size = new BoardSize(request.Columns, request.Rows);
        var placement = EnumNames<FleetPlacement>.Parse(request.FleetPlacement);
        var mode = EnumNames<GameMode>.Parse(request.Mode);

        try
        {
            Board HumanBoard() => placement is FleetPlacement.Manual
                ? new Board(size)
                : placer.Place(size, FleetTemplate.Standard);

            var first = new Player(request.PlayerName, isBot: false, HumanBoard());

            // En hot-seat les deux joueurs sont humains, donc les deux posent leur
            // flotte ; en Solo le serveur remplit toujours la grille du bot.
            var second = mode is GameMode.Local
                ? new Player(request.OpponentName!, isBot: false, HumanBoard())
                : new Player("Bot", isBot: true, placer.Place(size, FleetTemplate.Standard));

            var game = new Game(mode, first, second, EnumNames<BotDifficulty>.Parse(request.BotDifficulty));

            repository.Add(game);

            return TypedResults.Created($"/games/{game.Id}", game.ViewForClient().ToResponse());
        }
        catch (InvalidOperationException error)
        {
            return TypedResults.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static IResult GetGame(Guid id, IGameRepository repository) =>
        repository.Find(id) is { } game
            ? TypedResults.Ok(game.ViewForClient().ToResponse())
            : TypedResults.NotFound();

    private static IResult Fire(Guid id, FireRequest request, IGameRepository repository)
    {
        if (repository.Find(id) is not { } game)
        {
            return TypedResults.NotFound();
        }

        var outcome = game.FireFromClient(new Coordinates(request.Column, request.Row));

        return outcome.Rejection is { } rejection
            ? Refused(rejection)
            : TypedResults.Ok(outcome.ToResponse(game));
    }

    private static IResult PlayBotTurn(Guid id, IGameRepository repository, IBotStrategyFactory strategies)
    {
        if (repository.Find(id) is not { } game)
        {
            return TypedResults.NotFound();
        }

        var outcome = game.PlayBotTurn(strategies.For(game.BotDifficulty));

        return outcome.Rejection is { } rejection
            ? Refused(rejection)
            : TypedResults.Ok(outcome.ToResponse(game));
    }

    private static IResult PlaceFleet(Guid id, PlaceFleetRequest request, IGameRepository repository)
    {
        if (repository.Find(id) is not { } game)
        {
            return TypedResults.NotFound();
        }

        var outcome = game.PlaceFleetFromClient(
            [.. request.Ships.Select(ship => new ShipPlacement(
                EnumNames<ShipKind>.Parse(ship.Kind),
                new Coordinates(ship.Column, ship.Row),
                EnumNames<Orientation>.Parse(ship.Orientation)))]);

        return outcome.Rejection is { } rejection
            ? Refused(rejection)
            : TypedResults.Ok(game.ViewForClient().ToResponse());
    }

    private static IResult Refused(FleetRejection rejection) => rejection switch
    {
        FleetRejection.WrongComposition => TypedResults.Problem(
            "La flotte doit compter exactement un navire de chaque type.",
            statusCode: StatusCodes.Status400BadRequest),

        FleetRejection.OutOfBounds => TypedResults.Problem(
            "Un navire dépasse de la grille.",
            statusCode: StatusCodes.Status400BadRequest),

        FleetRejection.Overlap => TypedResults.Problem(
            "Deux navires se chevauchent.",
            statusCode: StatusCodes.Status400BadRequest),

        FleetRejection.FleetAlreadyPlaced => TypedResults.Problem(
            "La flotte est déjà posée.",
            statusCode: StatusCodes.Status409Conflict),

        _ => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };

    private static IResult Refused(FireRejection rejection) => rejection switch
    {
        FireRejection.FleetNotPlaced => TypedResults.Problem(
            "La flotte n'est pas encore posée.",
            statusCode: StatusCodes.Status409Conflict),

        FireRejection.OutsideBoard => TypedResults.Problem(
            "La case visée est en dehors de la grille.",
            statusCode: StatusCodes.Status400BadRequest),

        FireRejection.AlreadyTargeted => TypedResults.Problem(
            "Cette case a déjà été visée ; le tour n'est pas consommé.",
            statusCode: StatusCodes.Status409Conflict),

        FireRejection.GameFinished => TypedResults.Problem(
            "La partie est terminée.",
            statusCode: StatusCodes.Status409Conflict),

        FireRejection.NotTheClientTurn => TypedResults.Problem(
            "C'est au bot de jouer ; le client ne tire pas à sa place.",
            statusCode: StatusCodes.Status409Conflict),

        FireRejection.NotTheBotTurn => TypedResults.Problem(
            "Ce n'est pas au bot de jouer.",
            statusCode: StatusCodes.Status409Conflict),

        _ => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
}
