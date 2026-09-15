using BattleShip.Domain;
using BattleShip.Grpc;
using FluentValidation;
using Grpc.Core;

namespace BattleShip.API.Grpc;

public sealed class FireCommandValidator : AbstractValidator<FireCommand>
{
    public FireCommandValidator()
    {
        RuleFor(command => command.GameId).NotEmpty();
        RuleFor(command => command.Column).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Row).GreaterThanOrEqualTo(0);
    }
}

public sealed class BattleGrpcService(IGameRepository repository) : Battle.BattleBase
{
    public override Task<ShotOutcome> Fire(FireCommand request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.GameId, out var gameId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Identifiant de partie invalide."));
        }

        if (repository.Find(gameId) is not { } game)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Partie inconnue."));
        }

        var target = new Coordinates(request.Column, request.Row);
        var outcome = game.Fire(target);

        if (outcome.Rejection is { } rejection)
        {
            throw new RpcException(Refused(rejection));
        }

        return Task.FromResult(new ShotOutcome
        {
            Column = target.Column,
            Row = target.Row,
            Result = outcome.Result.ToString(),
            GameOver = game.Status is GameStatus.Finished,
            Winner = game.Winner?.Name ?? string.Empty
        });
    }

    private static Status Refused(FireRejection rejection) => rejection switch
    {
        FireRejection.OutsideBoard => new Status(
            StatusCode.InvalidArgument, "La case visée est en dehors de la grille."),

        FireRejection.AlreadyTargeted => new Status(
            StatusCode.FailedPrecondition, "Cette case a déjà été visée ; le tour n'est pas consommé."),

        FireRejection.GameFinished => new Status(
            StatusCode.FailedPrecondition, "La partie est terminée."),

        _ => new Status(StatusCode.Unknown, "Tir refusé.")
    };
}
