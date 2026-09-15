namespace BattleShip.Domain;

/// <summary>
/// Chasse puis traque. Tant qu'aucun navire n'est touché, le bot balaie la
/// grille ; dès qu'une case touchée appartient à un navire encore à flot, il
/// n'explore plus que le voisinage de cette case.
/// </summary>
public class HuntTargetBot(Random random) : IBotStrategy
{
    private static readonly (int Column, int Row)[] Steps = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public Coordinates ChooseTarget(GameView view, BoardSize size)
    {
        var fired = view.ShotsFired.Select(shot => shot.Target).ToHashSet();
        var damaged = view.ShotsFired.Where(shot => shot.Result is not ShotResult.Miss)
                                     .Select(shot => shot.Target)
                                     .ToHashSet();
        var sunk = view.ShotsFired.Where(shot => shot.Result is ShotResult.Sunk)
                                  .Select(shot => shot.Target)
                                  .ToHashSet();

        var pending = damaged.Where(cell => !BelongsToASunkShip(cell, sunk, damaged)).ToHashSet();

        return Track(pending, fired, size) ?? Hunt(fired, size, ScanStride(view));
    }

    /// <summary>
    /// Le pas du damier de balayage. 1 signifie « toutes les cases », ce que fait
    /// ce niveau-ci. <see cref="HuntTargetParityBot"/> le déduit de la flotte.
    /// Il ne s'applique qu'à la chasse : la traque doit pouvoir atteindre
    /// n'importe quel voisin d'une case touchée.
    /// </summary>
    protected virtual int ScanStride(GameView view) => 1;

    private Coordinates? Track(HashSet<Coordinates> pending, HashSet<Coordinates> fired, BoardSize size)
    {
        var candidates = pending
            .SelectMany(Around)
            .Distinct()
            .Where(cell => size.Contains(cell) && !fired.Contains(cell))
            .ToList();

        if (candidates.Count is 0)
        {
            return null;
        }

        var alongAKnownLine = candidates.Where(cell => ExtendsALine(cell, pending)).ToList();

        return Pick(alongAKnownLine.Count > 0 ? alongAKnownLine : candidates);
    }

    private Coordinates Hunt(HashSet<Coordinates> fired, BoardSize size, int stride)
    {
        var free = AllCells(size).Where(cell => !fired.Contains(cell)).ToList();

        if (free.Count is 0)
        {
            throw new InvalidOperationException("Aucune case disponible : la partie aurait dû être terminée.");
        }

        var scanned = free.Where(cell => (cell.Column + cell.Row) % stride is 0).ToList();

        return Pick(scanned.Count > 0 ? scanned : free);
    }

    /// <summary>
    /// Un navire occupe une ligne droite : toute case touchée d'un navire coulé
    /// est donc alignée avec la case qui l'a coulé, sans case intacte entre les
    /// deux. La réciproque n'est pas garantie — le contact étant autorisé, deux
    /// navires alignés et mitoyens se confondent. Le bot retourne alors chasser
    /// trop tôt ; il ne tire jamais un coup interdit.
    /// </summary>
    private static bool BelongsToASunkShip(
        Coordinates hit,
        HashSet<Coordinates> sunk,
        HashSet<Coordinates> damaged) =>
        sunk.Contains(hit) || sunk.Any(end => JoinedByDamagedCells(hit, end, damaged));

    private static bool JoinedByDamagedCells(Coordinates from, Coordinates to, HashSet<Coordinates> damaged)
    {
        if (from.Row == to.Row)
        {
            return Between(from.Column, to.Column).All(column => damaged.Contains(new Coordinates(column, from.Row)));
        }

        return from.Column == to.Column &&
               Between(from.Row, to.Row).All(row => damaged.Contains(new Coordinates(from.Column, row)));
    }

    private static IEnumerable<int> Between(int one, int other)
    {
        for (var value = Math.Min(one, other) + 1; value < Math.Max(one, other); value++)
        {
            yield return value;
        }
    }

    private static bool ExtendsALine(Coordinates cell, HashSet<Coordinates> pending) =>
        Steps.Any(step =>
            pending.Contains(Shift(cell, step, 1)) &&
            pending.Contains(Shift(cell, step, 2)));

    private static IEnumerable<Coordinates> Around(Coordinates cell) =>
        Steps.Select(step => Shift(cell, step, 1));

    private static Coordinates Shift(Coordinates cell, (int Column, int Row) step, int times) =>
        new(cell.Column + (step.Column * times), cell.Row + (step.Row * times));

    private static IEnumerable<Coordinates> AllCells(BoardSize size)
    {
        for (var column = 0; column < size.Columns; column++)
        {
            for (var row = 0; row < size.Rows; row++)
            {
                yield return new Coordinates(column, row);
            }
        }
    }

    private Coordinates Pick(IReadOnlyList<Coordinates> cells) => cells[random.Next(cells.Count)];
}

/// <summary>
/// Même traque, chasse moins coûteuse : un navire de longueur L croise
/// forcément une maille de pas L, jamais moins. Le pas suit donc le plus petit
/// navire de la flotte en jeu — 2 pour la flotte standard, 1 dès qu'un navire
/// n'occupe qu'une case, ce qui ramène le balayage à la grille entière.
///
/// Sans information de flotte, le pas vaut 1 : mieux vaut balayer trop que
/// manquer un navire. Voir ADR 0013.
/// </summary>
public sealed class HuntTargetParityBot(Random random) : HuntTargetBot(random)
{
    protected override int ScanStride(GameView view) =>
        view.Fleet.Count is 0 ? 1 : view.Fleet.Min(ship => ship.Size);
}
