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
        view.OpponentName,
        view.IsViewerTurn,
        [.. view.OwnFleet.Select(ship => new ShipDto(
            ship.Kind.ToString(),
            [.. ship.Cells.Select(ToDto)],
            [.. ship.Hits.Select(ToDto)],
            ship.IsSunk))],
        [.. view.ShotsReceived.Select(ToDto)],
        [.. view.ShotsFired.Select(cell => new RevealedCellDto(cell.Target.ToDto(), cell.Result.ToString()))],
        view.BotDifficulty.ToString(),
        [.. view.FleetToPlace.Select(ship => new ShipToPlaceDto(ship.Kind.ToString(), ship.Size))],
        [.. view.Fleet.Select(ship => new ShipToPlaceDto(ship.Kind.ToString(), ship.Size))],
        view.WinnerName);

    public static ShotOutcomeResponse ToResponse(this FireOutcome outcome, Game game) => new(
        outcome.Target.ToDto(),
        outcome.Result.ToString(),
        game.Status is GameStatus.Finished,
        game.Winner?.Name,
        game.ViewForClient().ToResponse());

    public static GameSummaryResponse ToResponse(this GameSummary summary) => new(
        summary.Id,
        summary.Mode.ToString(),
        summary.BotDifficulty.ToString(),
        summary.Status.ToString(),
        summary.FirstPlayer,
        summary.SecondPlayer,
        summary.WinnerName,
        summary.Shots,
        summary.StartedAt,
        summary.FinishedAt);

    public static StatisticsResponse ToResponse(this Statistics statistics) => new(
        statistics.Games,
        statistics.Finished,
        statistics.Shots,
        statistics.Hits,
        statistics.Sunk,
        statistics.Accuracy);
}
