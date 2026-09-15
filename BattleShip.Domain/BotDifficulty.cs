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
    public static bool TryParse(string? name, out BotDifficulty difficulty)
    {
        difficulty = default;

        if (name is null || !Names.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        difficulty = Enum.Parse<BotDifficulty>(name, ignoreCase: true);

        return true;
    }

    /// <summary>
    /// Pour les appelants situés derrière le filtre de validation (ADR 0006),
    /// qui garantit déjà un nom connu. Lève plutôt que de retomber sur une
    /// valeur par défaut : le jour où le filtre sauterait, l'appel échoue au
    /// lieu de servir silencieusement un bot dégradé.
    /// </summary>
    public static BotDifficulty Parse(string? name) =>
        TryParse(name, out var difficulty)
            ? difficulty
            : throw new ArgumentOutOfRangeException(nameof(name), name, "Difficulté de bot inconnue.");
}
