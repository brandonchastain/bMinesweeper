namespace BMinesweeper.Game;

/// <summary>
/// How thickly the mines are sown. A density rather than a count, because the board is
/// sized to the viewport — the same count that makes a phone board tense would leave a
/// desktop board nearly empty.
/// </summary>
public enum Difficulty
{
    /// <summary>Roughly one cell in ten, as the classic beginner board.</summary>
    Easy,

    /// <summary>Roughly one cell in six, as the classic intermediate board.</summary>
    Medium,

    /// <summary>Roughly one cell in five, as the classic expert board.</summary>
    Expert,
}
