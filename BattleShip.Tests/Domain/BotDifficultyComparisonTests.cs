using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

/// <summary>
/// Trois niveaux n'ont de sens que s'ils se classent. Ces tests mesurent le seul
/// indicateur qui définit la difficulté : le nombre de tirs nécessaires pour
/// couler une flotte. Ils échouent dès qu'une stratégie perd sa spécificité —
/// une traque cassée ramène le chasseur au niveau du hasard.
/// </summary>
public class BotDifficultyComparisonTests
{
    private static readonly double Chance = BotDrill.AverageShotsToClear(seed => new RandomBot(new Random(seed)));
    private static readonly double Hunt = BotDrill.AverageShotsToClear(seed => new HuntTargetBot(new Random(seed)));
    private static readonly double Parity = BotDrill.AverageShotsToClear(seed => new HuntTargetParityBot(new Random(seed)));

    [Fact]
    public void HuntTarget_ClearsAFleetInFewerShotsThanRandom() =>
        Assert.True(Hunt < Chance, $"traque {Hunt:F1} tirs, hasard {Chance:F1} tirs");

    [Fact]
    public void HuntTargetParity_ClearsAFleetInFewerShotsThanHuntTarget() =>
        Assert.True(Parity < Hunt, $"damier {Parity:F1} tirs, traque {Hunt:F1} tirs");

    /// <summary>
    /// Bornes larges, mesurées puis arrondies vers l'extérieur : elles ne pincent
    /// pas une valeur exacte, elles interdisent qu'un niveau dérive au point de
    /// se confondre avec un autre.
    /// </summary>
    [Theory]
    [InlineData(BotDifficulty.Random, 90, 100)]
    [InlineData(BotDifficulty.HuntTarget, 55, 75)]
    [InlineData(BotDifficulty.HuntTargetParity, 50, 70)]
    public void EachDifficulty_ClearsAFleetWithinItsExpectedRange(BotDifficulty difficulty, int floor, int ceiling)
    {
        var average = difficulty switch
        {
            BotDifficulty.Random => Chance,
            BotDifficulty.HuntTarget => Hunt,
            _ => Parity
        };

        Assert.InRange(average, floor, ceiling);
    }
}
