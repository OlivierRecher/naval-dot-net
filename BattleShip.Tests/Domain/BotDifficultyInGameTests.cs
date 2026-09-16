using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class BotDifficultyInGameTests
{
    private static Game SoloGame(BotDifficulty difficulty, int seed)
    {
        var placer = new RandomFleetPlacer(new Random(seed));
        var human = new Player("Humain", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var bot = new Player("Bot", isBot: true, placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        return new Game(GameMode.Solo, human, bot, difficulty);
    }

    [Theory]
    [InlineData(BotDifficulty.Random)]
    [InlineData(BotDifficulty.HuntTarget)]
    [InlineData(BotDifficulty.HuntTargetParity)]
    public void ASoloGame_AgainstEveryDifficulty_RunsToAWinner(BotDifficulty difficulty)
    {
        var game = SoloGame(difficulty, seed: 13);
        var strategy = new BotStrategyFactory(new Random(13)).For(difficulty);
        var nextHumanCell = 0;

        for (var turn = 0; turn < 400 && game.Status is GameStatus.InProgress; turn++)
        {
            if (game.CurrentPlayer.IsBot)
            {
                game.PlayBotTurn(strategy);
                continue;
            }

            var cell = new Coordinates(nextHumanCell % 10, (nextHumanCell / 10) % 10);
            nextHumanCell++;
            game.FireFromClient(cell);
        }

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.NotNull(game.Winner);
    }

    /// <summary>
    /// Construit sans le quatrième argument : passer <c>BotDifficulty.Random</c>
    /// explicitement rendrait le test insensible au défaut qu'il prétend fixer.
    /// </summary>
    [Fact]
    public void ANewGame_WithoutAnExplicitDifficulty_PlaysAtRandom()
    {
        var placer = new RandomFleetPlacer(new Random(1));
        var human = new Player("Humain", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var bot = new Player("Bot", isBot: true, placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        var game = new Game(GameMode.Solo, human, bot);

        Assert.Equal(BotDifficulty.Random, game.BotDifficulty);
    }

    [Fact]
    public void TheView_PublishesTheDifficulty_SoThePlayerKnowsWhatHeFaces() =>
        Assert.Equal(BotDifficulty.HuntTargetParity, SoloGame(BotDifficulty.HuntTargetParity, seed: 1).ViewForClient().BotDifficulty);
}
