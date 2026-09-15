namespace BattleShip.Domain;

/// <summary>
/// Qui pose la flotte de l'humain : le serveur au hasard, ou le joueur case par
/// case. Une partie <see cref="Manual"/> naît en <see cref="GameStatus.AwaitingFleet"/>.
/// </summary>
public enum FleetPlacement
{
    Random,
    Manual
}
