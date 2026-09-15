using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class BotStrategyFactoryTests
{
    private readonly IBotStrategyFactory _factory = new BotStrategyFactory(new Random(1));

    [Theory]
    [InlineData(BotDifficulty.Random, typeof(RandomBot))]
    [InlineData(BotDifficulty.HuntTarget, typeof(HuntTargetBot))]
    [InlineData(BotDifficulty.HuntTargetParity, typeof(HuntTargetParityBot))]
    public void For_MapsEachDifficultyToItsOwnStrategy(BotDifficulty difficulty, Type expected) =>
        Assert.Equal(expected, _factory.For(difficulty).GetType());

    [Fact]
    public void For_OnADifficultyOutsideTheEnumeration_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _factory.For((BotDifficulty)42));
}

public class EnumNamesTests
{
    [Theory]
    [InlineData("Random", BotDifficulty.Random)]
    [InlineData("HuntTarget", BotDifficulty.HuntTarget)]
    [InlineData("HuntTargetParity", BotDifficulty.HuntTargetParity)]
    [InlineData("huntTargetParity", BotDifficulty.HuntTargetParity)]
    public void TryParse_OnAKnownName_Succeeds(string name, BotDifficulty expected)
    {
        Assert.True(EnumNames<BotDifficulty>.TryParse(name, out var difficulty));
        Assert.Equal(expected, difficulty);
    }

    /// <summary>
    /// <c>Enum.TryParse</c> accepte la valeur numérique sous-jacente, y compris
    /// une valeur qui ne correspond à aucun membre : s'appuyer dessus laisserait
    /// entrer <c>"42"</c> comme niveau valide. Le contrat exposé au client est la
    /// liste des noms, pas la représentation entière de l'énumération.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("42")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Expert")]
    public void TryParse_OnAnythingButAName_Fails(string? candidate) =>
        Assert.False(EnumNames<BotDifficulty>.TryParse(candidate, out _));

    [Fact]
    public void Names_ListsEveryDifficulty() =>
        Assert.Equal(Enum.GetValues<BotDifficulty>().Length, EnumNames<BotDifficulty>.All.Count);
}
