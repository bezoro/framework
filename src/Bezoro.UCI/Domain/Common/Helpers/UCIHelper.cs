using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.Domain.Common.Helpers;

/// <summary>
///     Provides static helper methods for working with UCI and FEN notations.
///     This class contains pure functions with no side effects.
/// </summary>
internal static class UCIHelper
{
	/// <summary>
	///     Validates if a string is in proper algebraic notation for a single square.
	/// </summary>
	/// <param name="square">The square notation to validate.</param>
	/// <returns>True if valid, false otherwise.</returns>
	public static bool IsValidAlgebraicNotation(string square) =>
		!string.IsNullOrWhiteSpace(square) && UciConstants.Regex.AlgebraicNotationRegex.IsMatch(square);

	/// <summary>
	///     Validates if a string is a well-formed FEN string.
	/// </summary>
	/// <param name="fen">The FEN string to validate.</param>
	/// <returns>True if the FEN string is valid, otherwise false.</returns>
	public static bool IsValidFen(string fen) =>
		!string.IsNullOrWhiteSpace(fen) && UciConstants.Regex.FenRegex.IsMatch(fen);

	/// <summary>
	///     Validates if a string is a well-formed UCI move.
	/// </summary>
	/// <param name="move">The move string to validate.</param>
	/// <returns>True if the move is in valid UCI format, false otherwise.</returns>
	public static bool IsValidUciMove(string? move) =>
		!string.IsNullOrWhiteSpace(move) && UciConstants.Regex.UciMoveRegex.IsMatch(move);

	/// <summary>
	///     Extracts the active player color from a FEN string.
	/// </summary>
	/// <param name="fen">The FEN string to parse.</param>
	/// <returns>The active player color ('w' for white, 'b' for black), or null if the FEN is invalid.</returns>
	public static char? GetPlayerColorFromFen(string fen)
	{
		if (string.IsNullOrWhiteSpace(fen)) return null;

		// The active color is the second field in a FEN string.
		string[]? parts = fen.Split(' ');
		if (parts.Length < 2) return null;

		return parts[1].ToLower() switch
		{
			"w" => 'w',
			"b" => 'b',
			_   => null
		};
	}

	/// <summary>
	///     Extracts the best move from a UCI engine response.
	/// </summary>
	public static string? ParseBestMoveFromResponse(string bestMoveLine)
	{
		// Expected format is "bestmove <move> [ponder <move>]"
		string[] parts = bestMoveLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		return parts.Length > 1 ? parts[1] : null;
	}
}
