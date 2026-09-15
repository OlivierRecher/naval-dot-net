namespace BattleShip.Domain;

public readonly record struct GameSummary(
    Guid Id,
    GameMode Mode,
    BotDifficulty BotDifficulty,
    GameStatus Status,
    string FirstPlayer,
    string SecondPlayer,
    string? WinnerName,
    int Shots,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

/// <summary>
/// Les compteurs bruts. Les taux s'en déduisent : les stocker ouvrirait la porte
/// à une incohérence entre un total et sa moyenne.
/// </summary>
public readonly record struct Statistics(
    int Games,
    int Finished,
    int Shots,
    int Hits,
    int Sunk)
{
    public double Accuracy => Shots is 0 ? 0 : (double)(Hits + Sunk) / Shots;
}

/// <summary>
/// Lecture seule, distincte de <see cref="IGameRepository"/> : l'historique
/// interroge des colonnes, il ne reconstruit aucune partie. Les mêler ferait
/// rejouer des centaines de journaux pour afficher un tableau.
/// </summary>
public interface IGameHistory
{
    IReadOnlyList<GameSummary> Recent(int limit);

    Statistics Overall();
}
