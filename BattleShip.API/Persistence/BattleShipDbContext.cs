using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BattleShip.API.Persistence;

public sealed class BattleShipDbContext(DbContextOptions<BattleShipDbContext> options) : DbContext(options)
{
    public DbSet<GameRecord> Games => Set<GameRecord>();

    public DbSet<PlayerRecord> Players => Set<PlayerRecord>();

    public DbSet<ShipRecord> Ships => Set<ShipRecord>();

    public DbSet<ShotRecord> Shots => Set<ShotRecord>();

    private static readonly ValueConverter<DateTimeOffset, long> Instant = new(
        moment => moment.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<GameRecord>(game =>
        {
            game.HasKey(record => record.Id);
            game.Property(record => record.Id).ValueGeneratedNever();

            // SQLite refuse ORDER BY sur un DateTimeOffset — verifie par
            // execution. Stocke en ticks UTC, l'ordre chronologique devient
            // l'ordre numerique, et la resolution de 100 ns rend deux parties
            // ex aequo impossibles en pratique.
            game.Property(record => record.StartedAt).HasConversion(Instant);
            game.Property(record => record.FinishedAt).HasConversion(Instant);
            game.HasMany(record => record.Players).WithOne().HasForeignKey(record => record.GameId);
            game.HasMany(record => record.Shots).WithOne().HasForeignKey(record => record.GameId);
        });

        builder.Entity<PlayerRecord>(player =>
        {
            player.HasKey(record => record.Id);
            player.Property(record => record.Id).ValueGeneratedNever();
            player.HasMany(record => record.Ships).WithOne().HasForeignKey(record => record.PlayerId);
        });

        builder.Entity<ShipRecord>(ship => ship.HasKey(record => record.Id));

        builder.Entity<ShotRecord>(shot =>
        {
            shot.HasKey(record => record.Id);

            // Le journal est en ajout seul et son ordre porte le rejeu : deux
            // tirs ne peuvent pas partager un rang dans une meme partie.
            shot.HasIndex(record => new { record.GameId, record.Ordinal }).IsUnique();
        });
    }
}
