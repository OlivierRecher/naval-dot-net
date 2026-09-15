using Microsoft.EntityFrameworkCore;

namespace BattleShip.API.Persistence;

/// <summary>
/// <c>EnsureCreated</c> crée un schéma absent, il ne modifie jamais un schéma
/// existant. L'ADR 0012 jugeait cela suffisant « faute d'évolution de schéma à
/// rejouer » ; l'item 6 en a produit une, et une base créée avant lui n'a pas la
/// colonne <c>Fleet</c>. Cette mise à niveau est explicite, idempotente, et
/// volontairement minimale — au prochain changement de schéma, c'est aux
/// migrations EF qu'il faudra passer. Voir ADR 0013.
/// </summary>
public static class SchemaUpgrade
{
    private static readonly (string Table, string Column, string Definition)[] AddedColumns =
    [
        ("Games", "Fleet", "TEXT NOT NULL DEFAULT ''")
    ];

    public static void Apply(BattleShipDbContext db)
    {
        db.Database.EnsureCreated();

        foreach (var (table, column, definition) in AddedColumns)
        {
            if (!HasColumn(db, table, column))
            {
                db.Database.ExecuteSqlRaw($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};");
            }
        }
    }

    private static bool HasColumn(BattleShipDbContext db, string table, string column)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';";

        var opened = command.Connection!.State is not System.Data.ConnectionState.Open;
        if (opened)
        {
            command.Connection.Open();
        }

        try
        {
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }
        finally
        {
            if (opened)
            {
                command.Connection.Close();
            }
        }
    }
}
