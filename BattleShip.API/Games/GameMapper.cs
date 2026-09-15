using BattleShip.Domain;
using BattleShip.Models;

namespace BattleShip.API.Games;

internal static class GameMapper
{
    public static CoordinatesDto ToDto(this Coordinates cell) => new(cell.Column, cell.Row);

    public static GameViewResponse ToResponse(this GameView view) => new(
        view.GameId,
        view.Status.ToString(),
        view.Mode.ToString(),
        view.Size.Columns,
        view.Size.Rows,
        view.ViewerName,
        view.IsViewerTurn,
        [.. view.OwnFleet.Select(ship => new ShipDto(
            ship.Kind.ToString(),
            [.. ship.Cells.Select(ToDto)],
            [.. ship.Hits.Select(ToDto)],
            ship.IsSunk))],
        [.. view.ShotsReceived.Select(ToDto)],
        [.. view.ShotsFired.Select(cell => new RevealedCellDto(cell.Target.ToDto(), cell.Result.ToString()))],
        view.WinnerName);

    public static ShotOutcomeResponse ToResponse(this FireOutcome outcome, Game game) => new(
        outcome.Target.ToDto(),
        outcome.Result.ToString(),
        game.Status is GameStatus.Finished,
        game.Winner?.Name,
        game.ViewForClient().ToResponse());
}
