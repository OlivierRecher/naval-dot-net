namespace BattleShip.Domain;

/// <summary>
/// Le domaine exprime son besoin de stockage ; l'infrastructure le satisfait.
/// Aucun type de persistance ne traverse cette frontiere. Voir ADR 0004.
/// </summary>
public interface IGameRepository
{
    void Add(Game game);

    Game? Find(Guid id);

    /// <summary>
    /// Publie les changements d'une partie deja connue du depot. L'implementation
    /// en memoire n'a rien a y faire — elle detient deja la reference — et c'est
    /// exactement ce qui masquait le besoin jusqu'a la bascule vers SQLite.
    /// Voir ADR 0012 et REVUE-IA revue 8.
    /// </summary>
    void Save(Game game);
}
