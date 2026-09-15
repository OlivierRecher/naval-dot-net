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

        return routes;
    }

    private static IResult CreateGame(
        CreateGameRequest request,
        IGameRepository repository,
        RandomFleetPlacer placer)
    {
        var size = new BoardSize(request.Columns, request.Rows);

        try
        {
            var human = new Player(request.PlayerName, isBot: false, placer.Place(size, FleetTemplate.Standard));
            var bot = new Player("Bot", isBot: true, placer.Place(size, FleetTemplate.Standard));
            var game = new Game(GameMode.Solo, human, bot);

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

        var target = new Coordinates(request.Column, request.Row);
        var outcome = game.Fire(target);

        return outcome.Rejection is { } rejection
            ? Refused(rejection)
            : TypedResults.Ok(outcome.ToResponse(game, target));
    }

    private static IResult PlayBotTurn(Guid id, IGameRepository repository, IBotStrategy strategy)
    {
        if (repository.Find(id) is not { } game)
        {
            return TypedResults.NotFound();
        }

        if (game.Status is GameStatus.Finished)
        {
            return Refused(FireRejection.GameFinished);
        }

        if (!game.CurrentPlayer.IsBot)
        {
            return TypedResults.Problem(
                "Ce n'est pas au bot de jouer.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var target = strategy.ChooseTarget(game.ViewFor(game.CurrentPlayer), game.Opponent.Board.Size);
        var outcome = game.Fire(target);

        return outcome.Rejection is { } rejection
            ? Refused(rejection)
            : TypedResults.Ok(outcome.ToResponse(game, target));
    }

    private static IResult Refused(FireRejection rejection) => rejection switch
    {
        FireRejection.OutsideBoard => TypedResults.Problem(
            "La case visée est en dehors de la grille.",
            statusCode: StatusCodes.Status400BadRequest),

        FireRejection.AlreadyTargeted => TypedResults.Problem(
            "Cette case a déjà été visée ; le tour n'est pas consommé.",
            statusCode: StatusCodes.Status409Conflict),

        FireRejection.GameFinished => TypedResults.Problem(
            "La partie est terminée.",
            statusCode: StatusCodes.Status409Conflict),

        _ => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
}
