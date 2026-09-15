namespace BattleShip.Domain;

public sealed class Player(string name, bool isBot, Board board)
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; } = name;

    public bool IsBot { get; } = isBot;

    public Board Board { get; } = board;
}
