using System.Runtime.CompilerServices;

namespace BMinesweeper.Game;

/// <summary>
/// The game. Plain C# — no Blazor, no JS, no canvas. Everything about how a board is
/// generated, revealed, flagged, won, and lost belongs in here, where a test can reach it
/// without a browser.
/// </summary>
public sealed class Minesweeper
{
    private Cell[,] cells = new Cell[0, 0];
    private bool minesPlaced;
    private double width;
    private double height;
    private TimeSpan last;
    private HashSet<(int Column, int Row)> visitedCells = new();

    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public int Mines { get; private set; }

    /// <summary>How thickly this board was sown.</summary>
    public Difficulty Difficulty { get; private set; } = Difficulty.Medium;
    public BoardLayout Layout { get; private set; }
    public GameState State { get; private set; } = GameState.Playing;
    public bool NeedsRedraw { get; private set; } = true;
    public TimeSpan Elapsed { get; private set; }
    /// <summary>Mines the player has yet to account for: buried mines, less flags planted.</summary>
    public int MinesRemaining => this.Mines - this.Count(static c => c.Flagged && !c.Revealed);

    public int SpacesRemaining => (this.Columns * this.Rows) - this.Mines - this.Count(static c => !c.Mine && c.Revealed);
    public Cell this[int column, int row] => this.cells[column, row];

    /// <summary>Deals a fresh board. Mines are not placed until the first reveal.</summary>
    /// <param name="columns">Cells across.</param>
    /// <param name="rows">Cells down.</param>
    /// <param name="mines">How many mines to bury.</param>
    public void NewGame(int columns = 9, int rows = 9, int mines = 10)
    {
        this.Columns = columns;
        this.Rows = rows;
        this.Mines = Math.Clamp(mines, 0, (columns * rows) - 1);
        this.cells = new Cell[columns, rows];
        this.minesPlaced = false;
        this.State = GameState.Playing;
        this.Elapsed = TimeSpan.Zero;
        this.Relayout();
        this.NeedsRedraw = true;
        this.visitedCells.Clear();
    }

    /// <summary>The viewport changed size. Told in CSS pixels; DPI is the drawer's problem.</summary>
    /// <param name="width">Board width, in CSS pixels.</param>
    /// <param name="height">Board height, in CSS pixels.</param>
    public void Resize(double width, double height)
    {
        this.width = width;
        this.height = height;

        // The board is meant to fill the viewport, so the grid itself grows with it rather
        // than sitting in a fixed 9x9 island. Only an untouched board may be re-dealt: once
        // the mines are down, a URL bar sliding away must not throw the game away.
        if (!this.minesPlaced)
        {
            var (columns, rows) = FitGrid(width, height);

            if (columns != this.Columns || rows != this.Rows)
            {
                this.NewGame(columns, rows, MinesFor(columns, rows, this.Difficulty));
                return;
            }
        }

        this.Relayout();
        this.NeedsRedraw = true;
    }

    /// <summary>Deals a fresh board sized to the current viewport.</summary>
    /// <param name="difficulty">How thickly to sow the mines.</param>
    public void NewGame(Difficulty difficulty)
    {
        var (columns, rows) = FitGrid(this.width, this.height);
        this.Difficulty = difficulty;
        this.NewGame(columns, rows, MinesFor(columns, rows, difficulty));
    }

    /// <summary>Deals a fresh board sized to the current viewport, at the current difficulty.</summary>
    public void NewGame() => this.NewGame(this.Difficulty);

    /// <summary>One animation frame. The clock is the only thing that moves on its own.</summary>
    /// <param name="timestamp">Time since the page loaded.</param>
    public void Update(TimeSpan timestamp)
    {
        if (this.Columns == 0)
        {
            this.NewGame();
        }

        // The clock starts on the first reveal and stops when the game does, so an
        // untouched board and a finished one both sit still.
        if (this.State == GameState.Playing && this.minesPlaced && this.last != TimeSpan.Zero)
        {
            this.Elapsed += timestamp - this.last;
            this.NeedsRedraw = true;
        }

        this.last = timestamp;
    }

    /// <summary>Marks the picture as current. Called by the host after it draws.</summary>
    public void MarkClean() => this.NeedsRedraw = false;

    /// <summary>A left click, in board CSS pixels: reveal.</summary>
    /// <param name="x">Board x, in CSS pixels.</param>
    /// <param name="y">Board y, in CSS pixels.</param>
    public void OnClick(double x, double y)
    {
        if (this.State != GameState.Playing || this.Layout.HitTest(x, y) is not { } hit)
        {
            return;
        }

        var (column, row) = hit;

        // A flag is a blocker: it is the player's own note that this cell is not to be
        // opened, so a click aimed at it does nothing until the flag comes off.
        if (this.cells[column, row].Flagged)
        {
            return;
        }

        this.EnsureMines(column, row);

        // Reveal the cell — flood-fill the run of zeroes, lose on a mine, and win
        // once every cell that is not a mine is open.

        this.visitedCells.Clear();
        FloodFill(column, row);

        this.cells[column, row].Revealed = true;

        if (this.cells[column, row].Mine)
        {
            this.State = GameState.Lost;
            this.RevealMines();
        }
        else if (this.SpacesRemaining == 0)
        {
            this.State = GameState.Won;
            this.RevealMines();
        }

        this.NeedsRedraw = true;
    }

    private void RevealMines()
    {
        for (int i = 0; i < this.Columns; i++)
        {
            for (int j = 0; j < this.Rows; j++)
            {
                if (this.cells[i, j].Mine)
                {
                    this.cells[i, j].Revealed = true;
                }
            }
        }
    }

    /// <summary>A right click or long press, in board CSS pixels: flag.</summary>
    /// <param name="x">Board x, in CSS pixels.</param>
    /// <param name="y">Board y, in CSS pixels.</param>
    /// <returns>What the press did, so the host can answer it.</returns>
    public FlagResult OnFlag(double x, double y)
    {
        if (this.State != GameState.Playing || this.Layout.HitTest(x, y) is not { } hit)
        {
            return FlagResult.None;
        }

        var (column, row) = hit;

        if (this.cells[column, row].Revealed)
        {
            return FlagResult.None;
        }

        bool flagged = !this.cells[column, row].Flagged;
        this.cells[column, row].Flagged = flagged;
        this.NeedsRedraw = true;

        return flagged ? FlagResult.Placed : FlagResult.Removed;
    }

    /// <summary>A key press, by <c>KeyboardEvent.code</c>.</summary>
    /// <param name="code">The physical key.</param>
    public void OnKeyDown(string code)
    {
        if (code == "KeyN")
        {
            this.NewGame(this.Difficulty);
        }
    }

    /// <summary>
    /// Picks a grid that fills the viewport at a comfortable finger-sized cell. Clamped at
    /// both ends: a handful of cells is not a game, and a hundred is not a target.
    /// </summary>
    /// <param name="width">Board width, in CSS pixels.</param>
    /// <param name="height">Board height, in CSS pixels.</param>
    /// <returns>Columns and rows to deal.</returns>
    private static (int Columns, int Rows) FitGrid(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return (9, 9);
        }

        // Roughly a fingertip on a phone, a little larger on a desktop where the pointer is
        // precise but the window is wide enough that tiny cells would multiply endlessly.
        double target = Math.Min(width, height) < 600 ? 40 : 46;

        return (
            Math.Clamp((int)Math.Round((width - 24) / target), 5, 40),
            Math.Clamp((int)Math.Round((height - 24) / target), 5, 40));
    }

    /// <summary>How many mines a board of this size gets at this difficulty.</summary>
    /// <param name="columns">Cells across.</param>
    /// <param name="rows">Cells down.</param>
    /// <param name="difficulty">How thickly to sow them.</param>
    /// <returns>A mine count, never zero.</returns>
    private static int MinesFor(int columns, int rows, Difficulty difficulty)
    {
        double density = difficulty switch
        {
            Difficulty.Easy => 0.10,
            Difficulty.Expert => 0.21,
            _ => 0.16,
        };

        return Math.Max(1, (int)Math.Round(columns * rows * density));
    }

    /// <summary>
    /// Scatters the mines, avoiding the cell the player opened first — the opening click
    /// should always give them something to work with rather than a coin flip.
    /// </summary>
    /// <param name="safeColumn">Column of the first click.</param>
    /// <param name="safeRow">Row of the first click.</param>
    private void EnsureMines(int safeColumn, int safeRow)
    {
        if (this.minesPlaced)
        {
            return;
        }

        bool isSafe = true;
        int randCol = -1;
        int randRow = -1;

        for (int i = 0; i < this.Mines; i++)
        {
            isSafe = true;

            while (isSafe)
            {
                randCol = Random.Shared.Next(this.Columns);
                randRow = Random.Shared.Next(this.Rows);

                // re-roll if the random cell is the safe cell or already has a mine
                isSafe = Math.Abs(randCol - safeColumn) <= 1 && Math.Abs(randRow - safeRow) <= 1;
                isSafe |= this.cells[randCol, randRow].Mine;
            }

            this.cells[randCol, randRow].Mine = true;
        }

        this.visitedCells.Clear();
        FillAdjacent(0, 0);

        this.minesPlaced = true;
    }

    private void FillAdjacent(int col, int row)
    {
        if (visitedCells.Contains((col, row)))
        {
            return;
        }

        if (this.cells[col, row].Mine)
        {
            return;
        }
        
        // prevent recursions from coming back to this cell
        this.visitedCells.Add((col, row));

        int adjacentMines = 0;
        for (int c = col - 1; c <= col + 1; c++)
        {
            for (int r = row - 1; r <= row + 1; r++)
            {
                if (c >= 0 && c < this.Columns && r >= 0 && r < this.Rows)
                {
                    if (r != row || c != col)
                    {
                        FillAdjacent(c, r);
                    }

                    if (this.cells[c, r].Mine)
                    {
                        adjacentMines++;
                    }
                }
            }
        }

        this.cells[col, row].Adjacent = adjacentMines;
    }

    private void FloodFill(int col, int row)
    {
        if (visitedCells.Contains((col, row)))
        {
            return;
        }

        if (this.cells[col, row].Mine)
        {
            return;
        }

        // The flood stops at a flag rather than rolling over it. Losing a flag you placed
        // deliberately — and the reasoning behind it — to a spreading reveal is worse than
        // the cell staying shut, which you can always undo.
        if (this.cells[col, row].Flagged)
        {
            return;
        }

        if (this.cells[col, row].Adjacent > 0)
        {
            this.cells[col, row].Revealed = true;
            return;
        }

        this.cells[col, row].Revealed = true;
        this.visitedCells.Add((col, row));

        for (int c = col - 1; c <= col + 1; c++)
        {
            for (int r = row - 1; r <= row + 1; r++)
            {
                if (c < 0 || c >= this.Columns || r < 0 || r >= this.Rows)
                {
                    continue;
                }

                int cdiff = Math.Abs(col - c);
                int rdiff = Math.Abs(row - r);
                bool isSelf = r == row && c == col;
                bool isDiag = cdiff == rdiff;
                bool isNonZero = this.cells[c, r].Adjacent > 0;

                if (!isSelf && (!isDiag || isNonZero))
                {
                    FloodFill(c, r);
                }
            }
        }
    }

    private void Relayout()
    {
        if (this.Columns == 0 || this.Rows == 0 || this.width == 0 || this.height == 0)
        {
            return;
        }

        // Square cells, as large as the tighter axis allows, and centred. Whole pixels: a
        // grid on half-pixel boundaries is a grid with soft edges.
        const double Margin = 12;
        double size = Math.Max(8, Math.Floor(Math.Min(
            (this.width - (2 * Margin)) / this.Columns,
            (this.height - (2 * Margin)) / this.Rows)));

        this.Layout = new BoardLayout(
            OriginX: Math.Round((this.width - (size * this.Columns)) / 2),
            OriginY: Math.Round((this.height - (size * this.Rows)) / 2),
            CellSize: size,
            Columns: this.Columns,
            Rows: this.Rows,
            Gap: 1);
    }

    private int Count(Func<Cell, bool> predicate)
    {
        int n = 0;

        foreach (var cell in this.cells)
        {
            if (predicate(cell))
            {
                n++;
            }
        }

        return n;
    }
}
