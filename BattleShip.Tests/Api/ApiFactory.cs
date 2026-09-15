using BattleShip.API.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BattleShip.Tests.Api;

/// <summary>
/// Un SQLite en mémoire par classe de tests. C'est le <b>vrai</b> moteur, pas un
/// double : le schéma, les contraintes et le SQL des statistiques sont exercés
/// pour de bon, mais chaque classe part d'une base vide. La connexion est tenue
/// ouverte parce qu'une base SQLite en mémoire disparaît avec sa dernière
/// connexion.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<BattleShipDbContext>>();
            services.RemoveAll<BattleShipDbContext>();
            services.AddDbContext<BattleShipDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
