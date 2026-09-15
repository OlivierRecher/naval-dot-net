namespace BattleShip.Domain;

public enum BotDifficulty
{
    Random,
    HuntTarget,
    HuntTargetParity
}

public static class BotDifficulties
{
    public static IReadOnlyList<string> Names { get; } = Enum.GetNames<BotDifficulty>();

    /// <summary>
    /// Le contrat exposé au client est la liste des noms. <c>Enum.TryParse</c>
    /// accepte en plus la valeur numérique sous-jacente — y compris une valeur
    /// hors énumération, que <c>Enum.IsDefined</c> rattrape mais que <c>"0"</c>
    /// contourne. Le nom est donc comparé, pas parsé.
    /// </summary>
    public static bool TryParse(string? name, out BotDifficulty level)
    {
        level = default;

        if (name is null || !Names.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        level = Enum.Parse<BotDifficulty>(name, ignoreCase: true);

        return true;
    }
}
