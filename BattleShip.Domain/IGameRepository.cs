namespace BattleShip.Domain;

/// <summary>
/// Le domaine exprime son besoin de stockage ; l'infrastructure le satisfait.
/// Aucun type de persistance ne traverse cette frontiere. Voir ADR 0004.
/// </summary>
public interface IGameRepository
{
    void Add(Game game);

    Game? Find(Guid id);
}
