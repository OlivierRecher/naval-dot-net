using FluentValidation;

namespace BattleShip.API.Validation;

/// <summary>
/// Applique le validateur du corps de requete sans que l'endpoint ait a le
/// demander : un endpoint ne peut donc pas oublier sa validation.
/// Ecart assume a l'exemple du support, voir ADR 0006.
/// </summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.Arguments.OfType<T>().FirstOrDefault() is not { } candidate)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(candidate, context.HttpContext.RequestAborted);

        return result.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(result.ToDictionary());
    }
}

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        where T : class
        => builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
