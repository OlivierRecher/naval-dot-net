namespace BattleShip.Domain;

public enum FireRejection
{
    OutsideBoard,
    AlreadyTargeted,
    GameFinished,
    NotTheClientTurn,
    NotTheBotTurn
}

/// <summary>
/// <see cref="Target"/> n'a de sens que lorsque le tir est accepte : un rejet
/// ne designe aucune case consommee.
/// </summary>
public sealed record FireOutcome(FireRejection? Rejection, Coordinates Target, ShotResult Result)
{
    public bool IsAccepted => Rejection is null;

    public static FireOutcome Accepted(Coordinates target, ShotResult result) => new(null, target, result);

    public static FireOutcome Rejected(FireRejection reason) => new(reason, default, default);
}

public static class FireRules
{
    public static FireRejection? Validate(GameStatus status, Board targetBoard, Coordinates target)
    {
        if (status is GameStatus.Finished)
        {
            return FireRejection.GameFinished;
        }

        if (!targetBoard.Size.Contains(target))
        {
            return FireRejection.OutsideBoard;
        }

        return targetBoard.WasAlreadyTargeted(target)
            ? FireRejection.AlreadyTargeted
            : null;
    }
}
