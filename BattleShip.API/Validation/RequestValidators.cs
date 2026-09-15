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

        RuleFor(request => request.BotDifficulty)
            .Must(level => BotDifficulties.TryParse(level, out _))
            .WithMessage($"Niveau de bot inconnu : attendu {string.Join(", ", BotDifficulties.Names)}.");
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
