using BMinesweeper.Game;

namespace BMinesweeper.Tests;

/// <summary>
/// A starting point. The game is plain C#, so the rules can be tested without a browser —
/// arrange a board, act on it, and assert on what the drawer would see.
/// </summary>
public class BoardTests
{
    [Fact]
    public void NewGameDealsTheRequestedBoard()
    {
        var game = new Minesweeper();
        game.NewGame(columns: 16, rows: 16, mines: 40);

        Assert.Equal(16, game.Columns);
        Assert.Equal(16, game.Rows);
        Assert.Equal(40, game.Mines);
        Assert.Equal(GameState.Playing, game.State);
    }

    [Fact]
    public void FlaggingACellCountsAgainstTheMineCounter()
    {
        var game = new Minesweeper();
        game.NewGame();
        game.Resize(400, 400);

        var (x, y, size) = game.Layout.CellRect(0, 0);
        game.OnFlag(x + (size / 2), y + (size / 2));

        Assert.True(game[0, 0].Flagged);
        Assert.Equal(game.Mines - 1, game.MinesRemaining);
    }

    [Fact]
    public void HitTestMapsAPointBackToItsCell()
    {
        var game = new Minesweeper();
        game.NewGame();
        game.Resize(400, 400);

        var (x, y, size) = game.Layout.CellRect(3, 5);

        Assert.Equal((3, 5), game.Layout.HitTest(x + (size / 2), y + (size / 2)));
        Assert.Null(game.Layout.HitTest(-1, -1));
    }
}
