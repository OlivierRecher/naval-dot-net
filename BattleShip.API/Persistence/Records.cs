namespace BattleShip.API.Persistence;

/// <summary>
/// Ce qui est réellement écrit : les placements et le journal ordonné. Rien de
/// dérivé — cases touchées, navires coulés, tour courant — n'est stocké, parce
/// que tout se rejoue. Voir ADR 0012.
/// </summary>
public sealed class GameRecord
{
    public Guid Id { get; set; }

    public string Mode { get; set; } = string.Empty;

    public string BotDifficulty { get; set; } = string.Empty;

    /// <summary>
    /// La composition, en noms séparés par des virgules. Ce n'est pas une donnée
    /// relationnelle : c'est un gabarit, jamais interrogé ligne à ligne, et le
    /// normaliser ferait une table de plus pour un champ qu'on relit d'un bloc.
    /// </summary>
    public string Fleet { get; set; } = string.Empty;

    public int Columns { get; set; }

    public int Rows { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public string? WinnerName { get; set; }

    public List<PlayerRecord> Players { get; set; } = [];

    public List<ShotRecord> Shots { get; set; } = [];
}

public sealed class PlayerRecord
{
    public Guid Id { get; set; }

    public Guid GameId { get; set; }

    /// <summary>0 pour le joueur qui ouvre la partie : le rejeu dépend de cet ordre.</summary>
    public int Seat { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsBot { get; set; }

    public List<ShipRecord> Ships { get; set; } = [];
}

public sealed class ShipRecord
{
    public int Id { get; set; }

    public Guid PlayerId { get; set; }

    public string Kind { get; set; } = string.Empty;

    public int Column { get; set; }

    public int Row { get; set; }

    public string Orientation { get; set; } = string.Empty;
}

public sealed class ShotRecord
{
    public int Id { get; set; }

    public Guid GameId { get; set; }

    public int Ordinal { get; set; }

    public Guid ShooterId { get; set; }

    public int Column { get; set; }

    public int Row { get; set; }

    /// <summary>
    /// Donnée <b>dérivable</b> : le rejeu la recalcule et ne la lit jamais. Elle
    /// n'est stockée que pour rendre les statistiques interrogeables en SQL sans
    /// rejouer chaque partie. Un test vérifie qu'elle ne diverge pas.
    /// </summary>
    public string Result { get; set; } = string.Empty;
}
