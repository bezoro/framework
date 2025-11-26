namespace Bezoro.Chess.API.Types;

/// <summary>
///     Extension methods for <see cref="PlayerColor" />.
/// </summary>
public static class PlayerColorExtensions
{
    /// <summary>
    ///     Converts the color to the FEN active color character ('w' or 'b').
    /// </summary>
    public static char ToFenChar(this PlayerColor color) =>
		color == PlayerColor.White ? 'w' : 'b';

    /// <summary>
    ///     Creates a PlayerColor from a FEN active color character.
    /// </summary>
    public static PlayerColor FromFenChar(char c) =>
		c == 'w' ? PlayerColor.White : PlayerColor.Black;

    /// <summary>
    ///     Gets the opponent's color.
    /// </summary>
    public static PlayerColor Opponent(this PlayerColor color) =>
		color == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
}

/// <summary>
///     Represents the color of a chess player.
/// </summary>
public enum PlayerColor
{
	/// <summary>White plays first.</summary>
	White,

	/// <summary>Black plays second.</summary>
	Black
}
