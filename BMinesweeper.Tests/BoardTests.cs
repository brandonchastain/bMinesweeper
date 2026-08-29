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
    public void EveryCellKnowsHowManyMinesTouchIt()
    {
        // Mines fall randomly, and the counts used to be filled by a walk seeded at one
        // corner — so the board came out blank whenever that corner happened to be a mine,
        // and came out partly blank whenever a wall of mines cut the walk off from the
        // rest of the grid. Both are rare enough to need repeating to catch.
        for (int trial = 0; trial < 300; trial++)
        {
            var game = new Minesweeper();
            game.NewGame(columns: 9, rows: 9, mines: 16);
            game.Resize(400, 400);

            var (x, y, size) = game.Layout.CellRect(4, 4);
            game.OnClick(x + (size / 2), y + (size / 2));

            for (int column = 0; column < game.Columns; column++)
            {
                for (int row = 0; row < game.Rows; row++)
                {
                    if (game[column, row].Mine)
                    {
                        continue;
                    }

                    Assert.Equal(CountNeighbouringMines(game, column, row), game[column, row].Adjacent);
                }
            }
        }
    }

    private static int CountNeighbouringMines(Minesweeper game, int column, int row)
    {
        int mines = 0;

        for (int c = column - 1; c <= column + 1; c++)
        {
            for (int r = row - 1; r <= row + 1; r++)
            {
                bool inside = c >= 0 && c < game.Columns && r >= 0 && r < game.Rows;

                if (inside && !(c == column && r == row) && game[c, r].Mine)
                {
                    mines++;
                }
            }
        }

        return mines;
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
