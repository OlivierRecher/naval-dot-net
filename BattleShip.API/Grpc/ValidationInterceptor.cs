using FluentValidation;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace BattleShip.API.Grpc;

/// <summary>
/// Pendant gRPC du filtre d'endpoint HTTP : une methode de service ne peut pas
/// oublier sa validation. Voir ADR 0006.
/// </summary>
public sealed class ValidationInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var validator = context.GetHttpContext().RequestServices.GetService<IValidator<TRequest>>();

        if (validator is not null)
        {
            var result = await validator.ValidateAsync(request, context.CancellationToken);

            if (!result.IsValid)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    string.Join(" ", result.Errors.Select(error => error.ErrorMessage))));
            }
        }

        return await continuation(request, context);
    }
}
