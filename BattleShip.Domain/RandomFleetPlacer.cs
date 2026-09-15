namespace BattleShip.Domain;

public sealed class RandomFleetPlacer(Random random)
{
    private const int MaxAttemptsPerShip = 500;

    public Board Place(BoardSize size, IReadOnlyList<ShipKind> fleet)
    {
        var board = new Board(size);

        foreach (var kind in fleet)
        {
            if (!TryPlace(board, size, kind))
            {
                throw new InvalidOperationException(
                    $"Aucun placement valide trouvé pour {kind} sur une grille {size.Columns}x{size.Rows}.");
            }
        }

        return board;
    }

    private bool TryPlace(Board board, BoardSize size, ShipKind kind)
    {
        for (var attempt = 0; attempt < MaxAttemptsPerShip; attempt++)
        {
            var candidate = new ShipPlacement(
                kind,
                new Coordinates(random.Next(size.Columns), random.Next(size.Rows)),
                random.Next(2) is 0 ? Orientation.Horizontal : Orientation.Vertical);

            if (board.Place(candidate) is null)
            {
                return true;
            }
        }

        return false;
    }
}
