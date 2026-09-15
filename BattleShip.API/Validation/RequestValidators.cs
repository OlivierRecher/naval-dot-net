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

        // Chaque nom d'abord, la composition ensuite : inutile d'evaluer une
        // regle de flotte sur une liste dont un element n'est pas un navire.
        RuleForEach(request => request.Fleet)
            .Must(kind => EnumNames<ShipKind>.TryParse(kind, out _))
            .WithMessage($"Type de navire inconnu : attendu {string.Join(", ", EnumNames<ShipKind>.All)}.");

        RuleFor(request => request.Fleet)
            .Must(FleetRequestExtensions.FitsTheBoard)
            .When(request => request.Fleet is { Count: > 0 } fleet && fleet.All(kind => EnumNames<ShipKind>.TryParse(kind, out _)))
            .WithMessage("Cette flotte ne tient pas sur cette grille : navire trop long, trop de navires, ou grille trop remplie.");

        RuleFor(request => request.Mode)
            .Must(mode => EnumNames<GameMode>.TryParse(mode, out _))
            .WithMessage($"Mode de jeu inconnu : attendu {string.Join(", ", EnumNames<GameMode>.All)}.");

        // Le nom de l'adversaire n'a de sens qu'en hot-seat. En Solo, l'adversaire
        // est un bot que le serveur nomme lui-meme.
        RuleFor(request => request.OpponentName)
            .NotEmpty()
            .MaximumLength(40)
            .When(request => string.Equals(request.Mode, nameof(GameMode.Local), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Une partie locale oppose deux joueurs : le nom du second est obligatoire.");

        RuleFor(request => request.FleetPlacement)
            .Must(placement => EnumNames<FleetPlacement>.TryParse(placement, out _))
            .WithMessage($"Placement de flotte inconnu : attendu {string.Join(", ", EnumNames<FleetPlacement>.All)}.");

        RuleFor(request => request.BotDifficulty)
            .Must(difficulty => EnumNames<BotDifficulty>.TryParse(difficulty, out _))
            .WithMessage($"Difficulté de bot inconnue : attendu {string.Join(", ", EnumNames<BotDifficulty>.All)}.");
    }
}

public static class FleetRequestExtensions
{
    internal static bool FitsTheBoard(CreateGameRequest request, IReadOnlyList<string>? fleet) =>
        FleetTemplateRules.Validate(
            new BoardSize(request.Columns, request.Rows),
            [.. fleet!.Select(kind => EnumNames<ShipKind>.Parse(kind))]) is null;
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
