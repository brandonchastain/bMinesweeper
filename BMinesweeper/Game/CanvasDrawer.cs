using Blazor.Extensions.Canvas.Canvas2D;

namespace BMinesweeper.Game;

/// <summary>
/// Draws the game to a 2D canvas. Deliberately plain: every cell is a rectangle and a
/// glyph, drawn straight to the visible canvas. If a frame ever gets expensive, the fix is
/// the usual one — find what is not changing, draw it once to an off-screen canvas, and
/// blit it afterwards.
///
/// Not batched: Blazor.Extensions.Canvas' BeginBatchAsync/EndBatchAsync never completed
/// here, and since the frame loop waits on this call the whole loop stopped with it.
/// </summary>
public sealed class CanvasDrawer : IGameDrawer
{
    private static readonly string[] NumberColours =
    {
        "#000000", "#1976d2", "#388e3c", "#d32f2f", "#7b1fa2",
        "#c2185b", "#0097a7", "#424242", "#616161",
    };

    private readonly Canvas2DContext canvas;

    /// <summary>Initializes a new instance of the <see cref="CanvasDrawer"/> class.</summary>
    /// <param name="canvas">The visible canvas.</param>
    public CanvasDrawer(Canvas2DContext canvas) => this.canvas = canvas;

    /// <inheritdoc/>
    public async Task Draw(Minesweeper game)
    {
        var layout = game.Layout;

        await this.canvas.SetFillStyleAsync("#2b2b33");
        await this.canvas.FillRectAsync(0, 0, 4000, 4000);

        await this.canvas.SetTextAlignAsync(Blazor.Extensions.Canvas.Canvas2D.TextAlign.Center);
        await this.canvas.SetTextBaselineAsync(TextBaseline.Middle);
        await this.canvas.SetFontAsync($"bold {Math.Round(layout.CellSize * 0.55)}px system-ui, sans-serif");

        for (int row = 0; row < layout.Rows; row++)
        {
            for (int column = 0; column < layout.Columns; column++)
            {
                await this.DrawCell(game, game[column, row], layout, column, row);
            }
        }
        
        for (int row = 0; row < layout.Rows; row++)
        {
            for (int column = 0; column < layout.Columns; column++)
            {
                await this.DrawCellBorder(game, game[column, row], layout, column, row);
            }
        }
    }

    private async Task DrawCellBorder(Minesweeper game, Cell cell, BoardLayout layout, int column, int row)
    {
        var (x, y, size) = layout.CellRect(column, row);

        if (cell.Mine && game.State != GameState.Playing)
        {
            switch (game.State)
            {
                case GameState.Won:
                    await this.canvas.SetStrokeStyleAsync("#00AA11");
                    break;
                case GameState.Lost:
                    await this.canvas.SetStrokeStyleAsync("#ff0000");
                    break;
                default:
                    throw new Exception("unsupported game state.");
            }

            await this.canvas.SetLineWidthAsync(10);
            await this.canvas.StrokeRectAsync(x, y, size, size);
        }        
    }

    private async Task DrawCell(Minesweeper game, Cell cell, BoardLayout layout, int column, int row)
    {
        var (x, y, size) = layout.CellRect(column, row);

        await this.canvas.SetFillStyleAsync(cell.Revealed ? "#d6d6d9" : "#8b8b96");
        await this.canvas.FillRectAsync(x, y, size, size);

        if (cell.Flagged && !cell.Revealed)
        {
            await this.canvas.SetFillStyleAsync("#d32f2f");
            await this.canvas.FillTextAsync("⚑", x + (size / 2), y + (size / 2));
            return;
        }

        if (!cell.Revealed)
        {
            return;
        }

        if (cell.Mine)
        {
            await this.canvas.SetFillStyleAsync("#111111");
            await this.canvas.FillTextAsync("✹", x + (size / 2), y + (size / 2));
        }
        else if (cell.Adjacent > 0)
        {
            await this.canvas.SetFillStyleAsync(NumberColours[cell.Adjacent]);
            await this.canvas.FillTextAsync(
                cell.Adjacent.ToString(), x + (size / 2), y + (size / 2));
        }
    }
}
