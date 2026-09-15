namespace BattleShip.Domain;

public enum FleetTemplateError
{
    Empty,
    TooManyShips,
    ShipLongerThanTheBoard,
    TooDense
}

/// <summary>
/// Ce qu'une composition doit respecter pour qu'une grille puisse l'accueillir.
/// Fonction pure : elle ne place rien, elle écarte ce qui ne peut pas tenir.
/// Voir ADR 0013.
/// </summary>
public static class FleetTemplateRules
{
    public const int MaxShips = 15;

    /// <summary>
    /// Part de la grille que la flotte peut occuper. Le placement aléatoire
    /// procède par essais : au-delà d'une certaine densité il échoue, et il
    /// échoue de façon <b>aléatoire</b>, ce qui donnerait un refus qui dépend de
    /// la graine. Ce plafond rend le refus déterministe. Sa valeur est mesurée,
    /// pas choisie : voir <c>FleetTemplateRulesTests</c>.
    /// </summary>
    public const double MaxOccupancy = 1.0 / 3.0;

    public static FleetTemplateError? Validate(BoardSize size, IReadOnlyList<ShipKind> fleet)
    {
        if (fleet.Count is 0)
        {
            return FleetTemplateError.Empty;
        }

        if (fleet.Count > MaxShips)
        {
            return FleetTemplateError.TooManyShips;
        }

        if (fleet.Any(kind => kind.Size() > Math.Max(size.Columns, size.Rows)))
        {
            return FleetTemplateError.ShipLongerThanTheBoard;
        }

        var occupied = fleet.Sum(kind => kind.Size());

        return occupied > size.Columns * size.Rows * MaxOccupancy
            ? FleetTemplateError.TooDense
            : null;
    }
}
