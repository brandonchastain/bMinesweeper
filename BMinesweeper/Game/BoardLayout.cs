namespace BMinesweeper.Game;

/// <summary>
/// Where the grid sits on the board, in CSS pixels. The one place that turns a cell into a
/// rectangle and a pointer back into a cell, so the game and the drawer cannot disagree.
/// </summary>
/// <param name="OriginX">Left edge of the grid.</param>
/// <param name="OriginY">Top edge of the grid.</param>
/// <param name="CellSize">Width and height of one cell, gap included.</param>
/// <param name="Columns">Cells across.</param>
/// <param name="Rows">Cells down.</param>
/// <param name="Gap">Hairline between drawn cells.</param>
public readonly record struct BoardLayout(
    double OriginX,
    double OriginY,
    double CellSize,
    int Columns,
    int Rows,
    double Gap)
{
    /// <summary>Gets the rectangle a cell is drawn in.</summary>
    /// <param name="column">Cell column.</param>
    /// <param name="row">Cell row.</param>
    /// <returns>Top-left corner and side length, in CSS pixels.</returns>
    public (double X, double Y, double Size) CellRect(int column, int row) => (
        this.OriginX + (column * this.CellSize),
        this.OriginY + (row * this.CellSize),
        this.CellSize - this.Gap);

    /// <summary>Finds the cell under a board coordinate.</summary>
    /// <param name="x">Board x, in CSS pixels.</param>
    /// <param name="y">Board y, in CSS pixels.</param>
    /// <returns>The cell, or null if the point misses the grid.</returns>
    public (int Column, int Row)? HitTest(double x, double y)
    {
        if (this.CellSize <= 0)
        {
            return null;
        }

        int column = (int)Math.Floor((x - this.OriginX) / this.CellSize);
        int row = (int)Math.Floor((y - this.OriginY) / this.CellSize);

        return column >= 0 && column < this.Columns && row >= 0 && row < this.Rows
            ? (column, row)
            : null;
    }
}
