namespace BattleShip.Domain;

public readonly record struct Shot(Guid ShooterId, Coordinates Target, ShotResult Result);
