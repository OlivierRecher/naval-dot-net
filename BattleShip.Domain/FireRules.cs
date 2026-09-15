namespace BattleShip.Domain;

public enum FireRejection
{
    OutsideBoard,
    AlreadyTargeted,
    GameFinished
}

public sealed record FireOutcome(FireRejection? Rejection, ShotResult Result)
{
    public bool IsAccepted => Rejection is null;

    public static FireOutcome Accepted(ShotResult result) => new(null, result);

    public static FireOutcome Rejected(FireRejection reason) => new(reason, default);
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
