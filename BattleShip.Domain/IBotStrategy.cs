namespace BattleShip.Domain;

/// <summary>
/// Le bot ne reçoit qu'une <see cref="GameView"/> : exactement l'information
/// dont dispose un joueur humain. Il ne peut donc pas tricher, et les niveaux
/// de difficulté du backlog item 2 se branchent ici.
/// </summary>
public interface IBotStrategy
{
    Coordinates ChooseTarget(GameView view, BoardSize size);
}

public sealed class RandomBot(Random random) : IBotStrategy
{
    public Coordinates ChooseTarget(GameView view, BoardSize size)
    {
        var alreadyFired = view.ShotsFired.Select(cell => cell.Target).ToHashSet();

        var available = new List<Coordinates>();
        for (var column = 0; column < size.Columns; column++)
        {
            for (var row = 0; row < size.Rows; row++)
            {
                var cell = new Coordinates(column, row);
                if (!alreadyFired.Contains(cell))
                {
                    available.Add(cell);
                }
            }
        }

        if (available.Count is 0)
        {
            throw new InvalidOperationException("Aucune case disponible : la partie aurait dû être terminée.");
        }

        return available[random.Next(available.Count)];
    }
}
