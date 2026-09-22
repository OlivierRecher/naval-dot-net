using System.Reflection;
using BattleShip.API.Grpc;
using BattleShip.API.Validation;
using FluentValidation;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BattleShip.Tests.Api;

/// <summary>
/// L'ADR 0006 promet qu'« un oubli de validation devient impossible sur un
/// endpoint qui declare un corps de requete type ». Le filtre HTTP est pourtant
/// opt-in : omettre <c>.WithValidation&lt;T&gt;()</c> laisse l'endpoint passer,
/// silencieusement. Cote gRPC, l'intercepteur se tait quand aucun validateur
/// n'est enregistre.
///
/// Ces tests sont ce qui rend la promesse vraie : ils parcourent les routes
/// reellement construites, pas le code source, et echoueraient le jour de
/// l'oubli.
/// </summary>
public class ValidationCoverageTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Assembly Contracts = typeof(BattleShip.Models.CreateGameRequest).Assembly;

    private IServiceScope Started()
    {
        _ = factory.CreateClient();

        return factory.Services.CreateScope();
    }

    private static IEnumerable<(string Route, Type Body)> TypedBodiesOf(EndpointDataSource source) =>
        from endpoint in source.Endpoints
        let method = endpoint.Metadata.GetMetadata<MethodInfo>()
        where method is not null
        from parameter in method.GetParameters()
        where parameter.ParameterType.Assembly == Contracts
        select (endpoint.DisplayName ?? method.Name, parameter.ParameterType);

    [Fact]
    public void EveryEndpointDeclaringATypedBody_DeclaresItsValidationFilter()
    {
        using var scope = Started();
        var source = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();

        var unguarded = source.Endpoints
            .Select(endpoint => new
            {
                Route = endpoint.DisplayName,
                Bodies = (endpoint.Metadata.GetMetadata<MethodInfo>()?.GetParameters() ?? [])
                    .Select(parameter => parameter.ParameterType)
                    .Where(type => type.Assembly == Contracts)
                    .ToList(),
                Validated = endpoint.Metadata.GetOrderedMetadata<ValidatedBody>()
                    .Select(marker => marker.RequestType)
                    .ToList()
            })
            .SelectMany(entry => entry.Bodies
                .Where(body => !entry.Validated.Contains(body))
                .Select(body => $"{entry.Route} accepte {body.Name} sans WithValidation<{body.Name}>()"))
            .ToList();

        Assert.Empty(unguarded);
    }

    [Fact]
    public void EveryTypedBody_HasAValidatorRegistered()
    {
        using var scope = Started();
        var source = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();

        var orphans = TypedBodiesOf(source)
            .Where(entry => scope.ServiceProvider.GetService(
                typeof(IValidator<>).MakeGenericType(entry.Body)) is null)
            .Select(entry => $"{entry.Route} : aucun IValidator<{entry.Body.Name}>")
            .ToList();

        Assert.Empty(orphans);
    }

    /// <summary>
    /// L'intercepteur gRPC ne leve pas quand le validateur manque : il laisse
    /// simplement passer. C'est donc au demarrage qu'il faut constater que
    /// chaque message entrant en a un.
    /// </summary>
    [Fact]
    public void EveryGrpcRequest_HasAValidatorRegistered()
    {
        using var scope = Started();

        var requests = typeof(BattleGrpcService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.GetParameters())
            .Where(parameters => parameters.Length is 2 && parameters[1].ParameterType == typeof(ServerCallContext))
            .Select(parameters => parameters[0].ParameterType)
            .ToList();

        Assert.NotEmpty(requests);

        var orphans = requests
            .Where(request => scope.ServiceProvider.GetService(
                typeof(IValidator<>).MakeGenericType(request)) is null)
            .Select(request => $"aucun IValidator<{request.Name}>")
            .ToList();

        Assert.Empty(orphans);
    }
}
