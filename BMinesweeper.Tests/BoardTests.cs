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
    public void FlaggingReportsWhatThePressDid()
    {
        var game = new Minesweeper();
        game.NewGame();
        game.Resize(400, 400);

        var (x, y, size) = game.Layout.CellRect(0, 0);
        double centreX = x + (size / 2);
        double centreY = y + (size / 2);

        // The host buzzes the phone off this, so a press that changes nothing — here, one
        // that misses the grid — has to be distinguishable from one that plants a flag.
        Assert.Equal(FlagResult.Placed, game.OnFlag(centreX, centreY));
        Assert.Equal(FlagResult.Removed, game.OnFlag(centreX, centreY));
        Assert.Equal(FlagResult.None, game.OnFlag(-1, -1));
    }

    [Fact]
    public void AFlagBlocksTheFloodAndTheClickUnderIt()
    {
        var game = new Minesweeper();

        // An empty board, so the flood is guaranteed to want every cell — anything left
        // unrevealed was blocked on purpose rather than simply never reached.
        game.NewGame(columns: 9, rows: 9, mines: 0);
        game.Resize(400, 400);

        Centre(game, 8, 8, out double flagX, out double flagY);
        game.OnFlag(flagX, flagY);

        Centre(game, 0, 0, out double clickX, out double clickY);
        game.OnClick(clickX, clickY);

        Assert.True(game[0, 0].Revealed);
        Assert.False(game[8, 8].Revealed);

        // And the flag blocks a click aimed straight at it: the player has to take the
        // flag off first, which is the point of planting one.
        game.OnClick(flagX, flagY);
        Assert.False(game[8, 8].Revealed);
    }

    private static void Centre(Minesweeper game, int column, int row, out double x, out double y)
    {
        var (left, top, size) = game.Layout.CellRect(column, row);
        x = left + (size / 2);
        y = top + (size / 2);
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
