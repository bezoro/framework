using Bezoro.UCI.API.Common.Enums;

namespace Bezoro.Chess.API.Types;

/// <summary>
///     Extension methods for <see cref="PlayerColor" />.
/// </summary>
public static class PlayerColorExtensions
{
	/// <summary>
	///     Creates a PlayerColor from a FEN active color character.
	/// </summary>
	public static PlayerColor FromFenChar(char c) => c == 'w' ? PlayerColor.White : PlayerColor.Black;

	extension(PlayerColor color)
	{
		/// <summary>
		///     Converts the color to the FEN active color character ('w' or 'b').
		/// </summary>
		public char ToFenChar() => color == PlayerColor.White ? 'w' : 'b';

		/// <summary>
		///     Gets the opponent's color.
		/// </summary>
		public PlayerColor Opponent() =>
			color == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;

		public PieceColor ToPieceColor() => color == PlayerColor.White ? PieceColor.White : PieceColor.Black;
	}
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
