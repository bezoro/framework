using System;

namespace Bezoro.Core.Chess.Utils
{
	/// <summary>
	///     Provides utility methods for working with chess algebraic notation for squares.
	/// </summary>
	public static class AlgebraicNotationUtils
	{
		/// <summary>
		///     Converts an algebraic square notation string to a BoardPosition object (0-indexed file and rank).
		///     Example: "a1" returns new BoardPosition(0, 0).
		///     Example: "e4" returns new BoardPosition(4, 3).
		/// </summary>
		/// <param name="algebraicSquare">The algebraic notation string (e.g., "e4"). Case-insensitive.</param>
		/// <returns>A BoardPosition object with 0-indexed File and Rank.</returns>
		/// <exception cref="ArgumentNullException">If algebraicSquare is null or whitespace.</exception>
		/// <exception cref="ArgumentException">
		///     If algebraicSquare is not in a valid format (e.g., "a", "1e", "e0", "aa1", or rank
		///     exceeds <see cref="MaxParseableRankNumber" />).
		/// </exception>
		public static BoardPosition FromAlgebraic(string algebraicSquare, char maxFile = 'h', int maxRank = 8)
		{
			if (string.IsNullOrWhiteSpace(algebraicSquare))
			{
				throw new ArgumentNullException(
					nameof(algebraicSquare), "Algebraic square notation cannot be null or whitespace.");
			}

			var normalizedSquare = algebraicSquare.ToLowerInvariant();

			if (normalizedSquare.Length < 2)
			{
				throw new ArgumentException(
					"Algebraic square notation must be at least 2 characters long (e.g., 'a1').",
					nameof(algebraicSquare));
			}

			var fileChar = normalizedSquare[0];
			var rankPart = normalizedSquare[1..];

			if (fileChar < 'a' || fileChar > maxFile)
			{
				throw new ArgumentException(
					$"Invalid file character '{fileChar}' in notation '{algebraicSquare}'. Must be a letter 'a'-'z'.",
					nameof(algebraicSquare));
			}

			if (!int.TryParse(rankPart, out var rankNumber) || rankNumber < 1 || rankNumber > maxRank)
			{
				throw new ArgumentException(
					$"Invalid rank part '{rankPart}' in notation '{algebraicSquare}'. Must be a positive number between 1 and {maxRank}.",
					nameof(algebraicSquare));
			}

			var fileIndex = fileChar   - 'a';
			var rankIndex = rankNumber - 1;

			return new(fileIndex, rankIndex);
		}

		/// <summary>
		///     Converts a BoardPosition (0-indexed file and rank) to algebraic notation string.
		///     Example: new BoardPosition(0, 0) returns "a1".
		///     Example: new BoardPosition(4, 3) returns "e4".
		/// </summary>
		/// <param name="position">The BoardPosition object containing 0-indexed File and Rank.</param>
		/// <returns>The algebraic notation string for the square.</returns>
		/// <exception cref="ArgumentNullException">If position is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     If File or Rank in position are outside expected ranges for conversion
		///     (File 0-25, Rank non-negative).
		/// </exception>
		public static string ToAlgebraic(BoardPosition position)
		{
			if (position == null)
			{
				throw new ArgumentNullException(nameof(position));
			}

			return ToAlgebraic(position.Column, position.Rank);
		}

		/// <summary>
		///     Converts 0-indexed file and rank integers to an algebraic notation string.
		///     Example: (0, 0) returns "a1".
		///     Example: (4, 3) returns "e4".
		/// </summary>
		/// <param name="fileIndex">The 0-indexed file (0 for 'a', 1 for 'b', ..., 7 for 'h', etc.).</param>
		/// <param name="rankIndex">The 0-indexed rank (0 for rank '1', 1 for rank '2', ..., 7 for rank '8', etc.).</param>
		/// <returns>The algebraic notation string for the square.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		///     If fileIndex or rankIndex are outside expected ranges (fileIndex 0-25,
		///     rankIndex non-negative).
		/// </exception>
		public static string ToAlgebraic(int fileIndex, int rankIndex)
		{
			if (fileIndex is < 0 or > 25)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fileIndex), "File index must be between 0 ('a') and 25 ('z').");
			}

			if (rankIndex < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(rankIndex), "Rank index must be non-negative.");
			}

			var fileChar   = (char)('a' + fileIndex);
			var rankString = (rankIndex + 1).ToString();

			return $"{fileChar}{rankString}";
		}
	}
}
