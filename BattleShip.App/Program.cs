using BattleShip.App;
using BattleShip.App.Services;
using BattleShip.Grpc;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Adresse de l'API : origine distincte du front, d'ou la politique CORS cote serveur.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "https://localhost:7027";

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseAddress + "/") });

builder.Services.AddScoped(_ =>
{
    // Depuis le navigateur, gRPC passe par gRPC-Web. Voir ADR 0005.
    var channel = GrpcChannel.ForAddress(apiBaseAddress, new GrpcChannelOptions
    {
        HttpHandler = new GrpcWebHandler(new HttpClientHandler())
    });

    return new Battle.BattleClient(channel);
});

builder.Services.AddScoped<GameSession>();
builder.Services.AddScoped<OceanAudio>();

await builder.Build().RunAsync();
