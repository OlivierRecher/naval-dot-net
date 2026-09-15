using BattleShip.API.Games;
using BattleShip.API.Grpc;
using BattleShip.API.Validation;
using BattleShip.Domain;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

const string BlazorClientPolicy = "BlazorClient";

builder.Services.AddOpenApi();
builder.Services.AddGrpc(options => options.Interceptors.Add<ValidationInterceptor>());
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<IGameRepository, InMemoryGameRepository>();
builder.Services.AddSingleton<IBotStrategyFactory>(_ => new BotStrategyFactory(Random.Shared));
builder.Services.AddSingleton(_ => new RandomFleetPlacer(Random.Shared));
builder.Services.AddValidatorsFromAssemblyContaining<CreateGameRequestValidator>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["https://localhost:7142", "http://localhost:5019"];

builder.Services.AddCors(options => options.AddPolicy(BlazorClientPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    // gRPC-Web transporte son statut dans des en-tetes que le navigateur ne
    // lit que s'ils sont explicitement exposes. Voir ADR 0005.
    .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseGrpcWeb();
app.UseCors(BlazorClientPolicy);

app.MapGameEndpoints();
app.MapGrpcService<BattleGrpcService>().EnableGrpcWeb().RequireCors(BlazorClientPolicy);

app.Run();

// Rend l'hote accessible a WebApplicationFactory dans BattleShip.Tests.
public partial class Program;
