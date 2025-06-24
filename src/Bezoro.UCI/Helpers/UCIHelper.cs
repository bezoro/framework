using System.Text.RegularExpressions;

namespace Bezoro.UCI.Helpers
{
	/// <summary>
	///     Provides static helper methods for working with UCI and FEN notations.
	///     This class contains pure functions with no side effects.
	/// </summary>
	internal static class UCIHelper
	{
		// Regex for a single square in algebraic notation (e.g., "e4").
		private static readonly Regex AlgebraicNotationRegex = new(@"^[a-h][1-8]$", RegexOptions.Compiled);

		// Regex for a FEN string, validating all six fields.
		private static readonly Regex FenRegex = new(
			@"^([rnbqkpRNBQKP1-8]+\/){7}[rnbqkpRNBQKP1-8]+\s[bw]\s(-|K?Q?k?q?)\s(-|[a-h][36])\s\d+\s\d+$",
			RegexOptions.Compiled);
		// Regex for a UCI move (e.g., "e2e4" or "a7a8q" for promotion).
		private static readonly Regex UCIMoveRegex = new(@"^[a-h][1-8][a-h][1-8]([qrbn])?$", RegexOptions.Compiled);

		/// <summary>
		///     Validates if a string is in proper algebraic notation for a single square.
		/// </summary>
		/// <param name="square">The square notation to validate.</param>
		/// <returns>True if valid, false otherwise.</returns>
		public static bool IsValidAlgebraicNotation(string square) =>
			!string.IsNullOrWhiteSpace(square) && AlgebraicNotationRegex.IsMatch(square.ToLower());

		/// <summary>
		///     Validates if a string is a well-formed FEN string.
		/// </summary>
		/// <param name="fen">The FEN string to validate.</param>
		/// <returns>True if the FEN string is valid, otherwise false.</returns>
		public static bool IsValidFen(string fen) =>
			!string.IsNullOrWhiteSpace(fen) && FenRegex.IsMatch(fen);

		/// <summary>
		///     Validates if a string is a well-formed UCI move.
		/// </summary>
		/// <param name="move">The move string to validate.</param>
		/// <returns>True if the move is in valid UCI format, false otherwise.</returns>
		public static bool IsValidUciMove(string? move) =>
			!string.IsNullOrWhiteSpace(move) && UCIMoveRegex.IsMatch(move.ToLower());

		/// <summary>
		///     Extracts the active player color from a FEN string.
		/// </summary>
		/// <param name="fen">The FEN string to parse.</param>
		/// <returns>The active player color ('w' for white, 'b' for black), or null if the FEN is invalid.</returns>
		public static char? GetPlayerColorFromFen(string fen)
		{
			if (string.IsNullOrWhiteSpace(fen))
			{
				return null;
			}

			// The active color is the second field in a FEN string.
			string[]? parts = fen.Split(' ');
			if (parts.Length < 2)
			{
				return null;
			}

			return parts[1].ToLower() switch
			{
				"w" => 'w',
				"b" => 'b',
				_   => null
			};
		}
	}
}
