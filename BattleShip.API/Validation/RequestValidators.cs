using BattleShip.Domain;
using BattleShip.Models;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class CreateGameRequestValidator : AbstractValidator<CreateGameRequest>
{
    public const int MinBoardSide = 8;
    public const int MaxBoardSide = 20;

    public CreateGameRequestValidator()
    {
        RuleFor(request => request.PlayerName)
            .NotEmpty()
            .MaximumLength(40);

        RuleFor(request => request.Columns)
            .InclusiveBetween(MinBoardSide, MaxBoardSide);

        RuleFor(request => request.Rows)
            .InclusiveBetween(MinBoardSide, MaxBoardSide);

        RuleFor(request => request.FleetPlacement)
            .Must(placement => EnumNames<FleetPlacement>.TryParse(placement, out _))
            .WithMessage($"Placement de flotte inconnu : attendu {string.Join(", ", EnumNames<FleetPlacement>.All)}.");

        RuleFor(request => request.BotDifficulty)
            .Must(difficulty => EnumNames<BotDifficulty>.TryParse(difficulty, out _))
            .WithMessage($"Difficulté de bot inconnue : attendu {string.Join(", ", EnumNames<BotDifficulty>.All)}.");
    }
}

public sealed class FireRequestValidator : AbstractValidator<FireRequest>
{
    public FireRequestValidator()
    {
        RuleFor(request => request.Column).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Row).GreaterThanOrEqualTo(0);
    }
}

public sealed class PlaceFleetRequestValidator : AbstractValidator<PlaceFleetRequest>
{
    public PlaceFleetRequestValidator()
    {
        RuleFor(request => request.Ships).NotEmpty();

        RuleForEach(request => request.Ships).ChildRules(ship =>
        {
            ship.RuleFor(placement => placement.Kind)
                .Must(kind => EnumNames<ShipKind>.TryParse(kind, out _))
                .WithMessage($"Type de navire inconnu : attendu {string.Join(", ", EnumNames<ShipKind>.All)}.");

            ship.RuleFor(placement => placement.Orientation)
                .Must(orientation => EnumNames<Orientation>.TryParse(orientation, out _))
                .WithMessage($"Orientation inconnue : attendu {string.Join(", ", EnumNames<Orientation>.All)}.");

            ship.RuleFor(placement => placement.Column).GreaterThanOrEqualTo(0);
            ship.RuleFor(placement => placement.Row).GreaterThanOrEqualTo(0);
        });
    }
}
