using BattleShip.Domain;
using BattleShip.Models;

namespace BattleShip.Tests.Api;

/// <summary>
/// <c>BattleShip.App</c> ne reference pas <c>BattleShip.Domain</c> : le front ne
/// connait ces notions que par leur nom, ecrit par le serveur. Renommer une
/// valeur d'enumeration compile donc des deux cotes et casse l'ecran en silence.
/// Ce test est le seul endroit ou leur accord est verifie, comme il l'est deja
/// pour BotDifficultyCatalog et ShipCatalog.
/// </summary>
public class SharedContractNamesTests
{
    [Fact]
    public void TheSharedStatusNames_MatchTheDomain() =>
        Assert.Equal(EnumNames<GameStatus>.All, GameStatusNames.All);

    [Fact]
    public void TheSharedModeNames_MatchTheDomain() =>
        Assert.Equal(EnumNames<GameMode>.All, GameModeNames.All);

    [Fact]
    public void TheSharedShotResultNames_MatchTheDomain() =>
        Assert.Equal(EnumNames<ShotResult>.All, ShotResultNames.All);

    [Fact]
    public void TheSharedOrientationNames_MatchTheDomain() =>
        Assert.Equal(EnumNames<Orientation>.All, OrientationNames.All);
}
