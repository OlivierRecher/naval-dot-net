namespace BattleShip.Domain;

public readonly record struct BoardSize(int Columns, int Rows)
{
    public static BoardSize Standard { get; } = new(10, 10);

    public bool Contains(Coordinates cell) =>
        cell.Column >= 0 && cell.Column < Columns &&
        cell.Row >= 0 && cell.Row < Rows;
}
