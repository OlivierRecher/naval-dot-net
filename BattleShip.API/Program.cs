using BattleShip.API.Games;
using BattleShip.API.Persistence;
using BattleShip.API.Grpc;
using BattleShip.API.Validation;
using BattleShip.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string BlazorClientPolicy = "BlazorClient";

builder.Services.AddOpenApi();
builder.Services.AddGrpc(options => options.Interceptors.Add<ValidationInterceptor>());
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<BattleShipDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("BattleShip") ?? "Data Source=battleship.db"));

// Le depot est Scoped, comme le DbContext qu'il porte. Le cache des parties
// vivantes reste Singleton : c'est lui qui garantit qu'une partie n'existe
// qu'en un exemplaire, donc que le verrou de l'agregat sert encore. ADR 0012.
builder.Services.AddSingleton<GameCache>();
builder.Services.AddScoped<IGameRepository, SqliteGameRepository>();
builder.Services.AddScoped<IGameHistory, SqliteGameHistory>();
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

using (var scope = app.Services.CreateScope())
{
    // Pas de migrations : le schema est cree au demarrage s'il manque. Le
    // periemetre ne comporte aucune evolution de schema a rejouer. ADR 0012.
    scope.ServiceProvider.GetRequiredService<BattleShipDbContext>().Database.EnsureCreated();
}

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
