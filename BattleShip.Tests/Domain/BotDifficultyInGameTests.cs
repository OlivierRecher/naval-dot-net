using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class BotDifficultyInGameTests
{
    private static Game SoloGame(BotDifficulty level, int seed)
    {
        var placer = new RandomFleetPlacer(new Random(seed));
        var human = new Player("Humain", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var bot = new Player("Bot", isBot: true, placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        return new Game(GameMode.Solo, human, bot, level);
    }

    [Theory]
    [InlineData(BotDifficulty.Random)]
    [InlineData(BotDifficulty.HuntTarget)]
    [InlineData(BotDifficulty.HuntTargetParity)]
    public void ASoloGame_AgainstEveryLevel_RunsToAWinner(BotDifficulty level)
    {
        var game = SoloGame(level, seed: 13);
        var strategy = new BotStrategyFactory(new Random(13)).For(level);
        var cells = Enumerable.Range(0, 100).Select(index => new Coordinates(index % 10, index / 10));

        foreach (var cell in cells)
        {
            if (game.Status is GameStatus.Finished)
            {
                break;
            }

            game.FireFromClient(cell);
            game.PlayBotTurn(strategy);
        }

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.NotNull(game.Winner);
    }

    [Fact]
    public void ANewGame_WithoutAnExplicitLevel_PlaysAtRandom() =>
        Assert.Equal(BotDifficulty.Random, SoloGame(BotDifficulty.Random, seed: 1).BotDifficulty);

    [Fact]
    public void TheView_PublishesTheLevel_SoThePlayerKnowsWhatHeFaces() =>
        Assert.Equal(BotDifficulty.HuntTargetParity, SoloGame(BotDifficulty.HuntTargetParity, seed: 1).ViewForClient().BotDifficulty);
}
