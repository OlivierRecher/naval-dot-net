namespace BattleShip.Domain;

/// <summary>
/// <paramref name="id"/> n'est fourni qu'a la relecture d'une partie persistee :
/// le journal de tirs designe ses tireurs par cet identifiant, il doit donc
/// survivre au redemarrage. Voir ADR 0012.
/// </summary>
public sealed class Player(string name, bool isBot, Board board, Guid? id = null)
{
    public Guid Id { get; } = id ?? Guid.NewGuid();

    public string Name { get; } = name;

    public bool IsBot { get; } = isBot;

    public Board Board { get; } = board;
}
