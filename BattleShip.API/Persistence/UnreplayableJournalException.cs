namespace BattleShip.API.Persistence;

/// <summary>
/// Le journal ne porte que des coordonnées : le rejeu recalcule qui tirait, donc
/// il dépend des règles du jeu. Une partie enregistrée sous d'autres règles n'est
/// plus reconstructible — elle se refuse au lieu de remonter en erreur serveur,
/// et elle ne se corrige pas en silence. Voir ADR 0014.
/// </summary>
public sealed class UnreplayableJournalException(Guid gameId, Exception cause)
    : Exception($"La partie {gameId} a été enregistrée sous d'autres règles : son journal ne se rejoue plus.", cause)
{
    public Guid GameId { get; } = gameId;
}
