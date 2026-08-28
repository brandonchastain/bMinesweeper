namespace BMinesweeper.Game;

/// <summary>
/// The rendering seam. The game says what is on the board; something else decides what
/// that looks like — which is what lets the rules be tested without a canvas.
/// </summary>
public interface IGameDrawer
{
    /// <summary>Paints the current position.</summary>
    /// <param name="game">The game to draw.</param>
    /// <returns>A task that completes when the frame has been issued.</returns>
    Task Draw(Minesweeper game);
}
