using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

/// <summary>
/// Le tour ne passe qu'au coup manqué (AGENTS.md § 3). Un test qui alterne en
/// aveugle réclame donc au bot un tour que le domaine refuse — et le refus passe
/// inaperçu, parce que <see cref="Game.PlayBotTurn"/> rend un
/// <see cref="FireOutcome"/> au lieu de lever. La partie jouée ne serait plus
/// celle que le test croit décrire.
/// </summary>
internal static class Rounds
{
    /// <summary>Le tir du client, puis ceux du bot tant qu'il garde la main.</summary>
    internal static void PlayRound(this Game game, IBotStrategy strategy, Coordinates target)
    {
        game.FireFromClient(target);

        while (game.Status is GameStatus.InProgress && game.CurrentPlayer.IsBot)
        {
            game.PlayBotTurn(strategy);
        }
    }

    /// <summary>
    /// Une grille dont on connaît le contenu : les règles de tour se vérifient
    /// sur des cases dont on sait à l'avance si elles portent un navire, pas sur
    /// un placement aléatoire dont le test dépendrait sans le dire.
    /// </summary>
    internal static Board BoardWith(params ShipPlacement[] placements)
    {
        var board = new Board(BoardSize.Standard);

        foreach (var placement in placements)
        {
            Assert.Null(board.Place(placement));
        }

        return board;
    }
}
