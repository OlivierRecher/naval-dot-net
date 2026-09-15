using BattleShip.Domain;

namespace BattleShip.Tests.Domain;

public class RandomBotTests
{
    private static Game GameWhereTheBotPlays()
    {
        var placer = new RandomFleetPlacer(new Random(5));
        var human = new Player("Humain", isBot: false, placer.Place(BoardSize.Standard, FleetTemplate.Standard));
        var bot = new Player("Bot", isBot: true, placer.Place(BoardSize.Standard, FleetTemplate.Standard));

        var game = new Game(GameMode.Solo, bot, human);
        return game;
    }

    [Fact]
    public void ChooseTarget_AlwaysPicksACellInsideTheBoard()
    {
        var game = GameWhereTheBotPlays();
        var bot = new RandomBot(new Random(3));

        for (var turn = 0; turn < 50; turn++)
        {
            var target = bot.ChooseTarget(game.ViewFor(game.CurrentPlayer), BoardSize.Standard);

            Assert.True(BoardSize.Standard.Contains(target), $"case {target} hors grille");
            game.Fire(target);
            if (game.Status is GameStatus.Finished) break;
            game.Fire(bot.ChooseTarget(game.ViewFor(game.CurrentPlayer), BoardSize.Standard));
            if (game.Status is GameStatus.Finished) break;
        }
    }

    [Fact]
    public void ChooseTarget_NeverPicksACellItHasAlreadyTargeted()
    {
        var game = GameWhereTheBotPlays();
        var bot = new RandomBot(new Random(11));
        var alreadyPlayed = new HashSet<Coordinates>();

        // Le bot ne connait que sa GameView, exactement comme un humain : s'il
        // rejouait une case, la partie se bloquerait sur des tirs refuses.
        for (var turn = 0; turn < 40 && game.Status is GameStatus.InProgress; turn++)
        {
            var view = game.ViewFor(game.CurrentPlayer);
            if (view.ViewerName != "Bot")
            {
                game.Fire(new Coordinates(turn % 10, turn / 10));
                continue;
            }

            var target = bot.ChooseTarget(view, BoardSize.Standard);

            Assert.True(alreadyPlayed.Add(target), $"le bot a rejoue la case {target}");
            Assert.True(game.Fire(target).IsAccepted, $"tir refuse en {target}");
        }
    }

    [Fact]
    public void ChooseTarget_WhenOnlyOneCellRemains_PicksIt()
    {
        var human = new Player("Humain", isBot: false,
            new RandomFleetPlacer(new Random(2)).Place(new BoardSize(2, 1), [ShipKind.Destroyer]));
        var bot = new Player("Bot", isBot: true,
            new RandomFleetPlacer(new Random(2)).Place(new BoardSize(2, 1), [ShipKind.Destroyer]));

        var game = new Game(GameMode.Solo, bot, human);
        var strategy = new RandomBot(new Random(9));
        var size = new BoardSize(2, 1);

        game.Fire(new Coordinates(0, 0));   // le bot joue
        game.Fire(new Coordinates(0, 0));   // l'humain joue

        Assert.Equal(new Coordinates(1, 0), strategy.ChooseTarget(game.ViewFor(game.CurrentPlayer), size));
    }
}
