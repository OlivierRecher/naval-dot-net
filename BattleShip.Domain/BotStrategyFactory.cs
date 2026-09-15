namespace BattleShip.Domain;

public interface IBotStrategyFactory
{
    IBotStrategy For(BotDifficulty difficulty);
}

/// <summary>
/// Le niveau est une donnée de la partie, la stratégie est un service : cette
/// fabrique est le seul endroit où les deux se rencontrent. <see cref="Game"/>
/// n'a donc jamais à connaître une implémentation de <see cref="IBotStrategy"/>.
/// </summary>
public sealed class BotStrategyFactory(Random random) : IBotStrategyFactory
{
    public IBotStrategy For(BotDifficulty difficulty) => difficulty switch
    {
        BotDifficulty.Random => new RandomBot(random),
        BotDifficulty.HuntTarget => new HuntTargetBot(random),
        BotDifficulty.HuntTargetParity => new HuntTargetParityBot(random),
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Difficulté de bot inconnue.")
    };
}
