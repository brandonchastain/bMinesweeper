namespace BMinesweeper.Game;

/// <summary>One square of the grid. A struct: the board is a dense array of these.</summary>
public struct Cell
{
    /// <summary>Gets or sets a value indicating whether a mine is buried here.</summary>
    public bool Mine { get; set; }

    /// <summary>Gets or sets a value indicating whether the player has opened this cell.</summary>
    public bool Revealed { get; set; }

    /// <summary>Gets or sets a value indicating whether the player has flagged this cell.</summary>
    public bool Flagged { get; set; }

    /// <summary>Gets or sets how many of the eight neighbours hold a mine.</summary>
    public int Adjacent { get; set; }
}
