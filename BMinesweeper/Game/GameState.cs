namespace BMinesweeper.Game;

/// <summary>Where the game as a whole stands.</summary>
public enum GameState
{
    /// <summary>Still going.</summary>
    Playing,

    /// <summary>Every safe cell is open.</summary>
    Won,

    /// <summary>A mine was opened.</summary>
    Lost,
}
