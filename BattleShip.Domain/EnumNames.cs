namespace BattleShip.Domain;

/// <summary>
/// Le contrat exposé au client est la <b>liste des noms</b> d'une énumération.
/// <c>Enum.TryParse</c> ne convient pas : il accepte aussi la valeur numérique
/// sous-jacente, y compris hors énumération — <c>Enum.IsDefined</c> rattrape
/// <c>"42"</c> mais pas <c>"0"</c>. Le nom est donc comparé, jamais parsé.
/// Voir ADR 0009.
/// </summary>
public static class EnumNames<T> where T : struct, Enum
{
    public static IReadOnlyList<string> All { get; } = Enum.GetNames<T>();

    public static bool TryParse(string? name, out T value)
    {
        value = default;

        if (name is null || !All.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        value = Enum.Parse<T>(name, ignoreCase: true);

        return true;
    }

    /// <summary>
    /// Pour les appelants situés derrière le filtre de validation (ADR 0006),
    /// qui garantit déjà un nom connu. Lève plutôt que de retomber sur une
    /// valeur par défaut : le jour où le filtre sauterait, l'appel échoue au
    /// lieu de servir silencieusement une partie mal configurée.
    /// </summary>
    public static T Parse(string? name) =>
        TryParse(name, out var value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(name), name, $"Valeur inconnue pour {typeof(T).Name}.");
}
