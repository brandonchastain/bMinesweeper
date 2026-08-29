namespace BMinesweeper.Game;

/// <summary>What a flag press actually did. The host turns this into haptic feedback.</summary>
public enum FlagResult
{
    /// <summary>Nothing: the press missed the grid, or landed on an open cell.</summary>
    None,

    /// <summary>A flag went down.</summary>
    Placed,

    /// <summary>A flag came back up.</summary>
    Removed,
}
