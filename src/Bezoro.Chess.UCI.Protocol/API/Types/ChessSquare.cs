using System;
using System.Globalization;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents one square on a standard chess board using zero-based indexes.
/// </summary>
[Serializable]
public readonly struct ChessSquare : IEquatable<ChessSquare>
{
	/// <summary>
	///     Number of files and ranks on a standard chess board.
	/// </summary>
	public const int BoardSize = 8;

	/// <summary>
	///     Creates a square from zero-based indexes where <c>0,0</c> is <c>a1</c>.
	/// </summary>
	/// <param name="fileIndex">Zero-based file index, where <c>0</c> is file <c>a</c>.</param>
	/// <param name="rankIndex">Zero-based rank index, where <c>0</c> is rank <c>1</c>.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when either index is outside the board.</exception>
	public ChessSquare(int fileIndex, int rankIndex)
	{
		if (!IsValidIndex(fileIndex))
			throw new ArgumentOutOfRangeException(
				nameof(fileIndex),
				fileIndex,
				"File index must be between 0 and 7."
			);

		if (!IsValidIndex(rankIndex))
			throw new ArgumentOutOfRangeException(
				nameof(rankIndex),
				rankIndex,
				"Rank index must be between 0 and 7."
			);

		FileIndex = fileIndex;
		RankIndex = rankIndex;
	}

	/// <summary>
	///     Gets the algebraic file letter from <c>a</c> through <c>h</c>.
	/// </summary>
	public char File => (char)('a' + FileIndex);

	/// <summary>
	///     Gets the zero-based file index, where <c>0</c> is file <c>a</c>.
	/// </summary>
	public int FileIndex { get; }

	/// <summary>
	///     Gets the one-based algebraic rank from <c>1</c> through <c>8</c>.
	/// </summary>
	public int Rank => RankIndex + 1;

	/// <summary>
	///     Gets the zero-based rank index, where <c>0</c> is rank <c>1</c>.
	/// </summary>
	public int RankIndex { get; }

	/// <summary>
	///     Compares two squares by file and rank index.
	/// </summary>
	/// <param name="left">First square.</param>
	/// <param name="right">Second square.</param>
	public static bool operator ==(ChessSquare left, ChessSquare right) => left.Equals(right);

	/// <summary>
	///     Compares two squares by file and rank index.
	/// </summary>
	/// <param name="left">First square.</param>
	/// <param name="right">Second square.</param>
	public static bool operator !=(ChessSquare left, ChessSquare right) => !left.Equals(right);

	/// <summary>
	///     Attempts to create a square without throwing for out-of-board indexes.
	/// </summary>
	/// <param name="fileIndex">Zero-based file index, where <c>0</c> is file <c>a</c>.</param>
	/// <param name="rankIndex">Zero-based rank index, where <c>0</c> is rank <c>1</c>.</param>
	/// <param name="square">Created square when both indexes are valid.</param>
	/// <returns><see langword="true" /> when both indexes are inside the standard board.</returns>
	public static bool TryCreate(int fileIndex, int rankIndex, out ChessSquare square)
	{
		if (!IsValidIndex(fileIndex) || !IsValidIndex(rankIndex))
		{
			square = default;
			return false;
		}

		square = new(fileIndex, rankIndex);
		return true;
	}

	/// <summary>
	///     Attempts to parse a two-character algebraic square such as <c>e4</c>.
	/// </summary>
	/// <param name="value">Square in file/rank notation. File matching is case-insensitive.</param>
	/// <param name="square">Parsed square when the input is valid.</param>
	/// <returns><see langword="true" /> when <paramref name="value" /> names a board square.</returns>
	public static bool TryParse(string? value, out ChessSquare square)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			square = default;
			return false;
		}

		string normalized = value.Trim();
		if (normalized.Length != 2)
		{
			square = default;
			return false;
		}

		char file = char.ToLowerInvariant(normalized[0]);
		char rank = normalized[1];
		int fileIndex = file - 'a';
		int rankIndex = rank - '1';

		return TryCreate(fileIndex, rankIndex, out square);
	}

	/// <summary>
	///     Determines whether this square is the promotion rank for the supplied side.
	/// </summary>
	/// <param name="color">Side to evaluate: <c>w</c> for white or <c>b</c> for black.</param>
	/// <returns><see langword="true" /> when this square is that side's final rank.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="color" /> is not <c>w</c> or <c>b</c>.</exception>
	public bool IsPromotionRankFor(char color)
	{
		return char.ToLowerInvariant(color) switch
		{
			'w' => RankIndex == BoardSize - 1,
			'b' => RankIndex == 0,
			_ => throw new ArgumentException(
				"Invalid chess side. Expected 'w' or 'b'.",
				nameof(color)
			)
		};
	}

	/// <summary>
	///     Compares this square with another square by zero-based file and rank indexes.
	/// </summary>
	/// <param name="other">Square to compare.</param>
	public bool Equals(ChessSquare other) => FileIndex == other.FileIndex && RankIndex == other.RankIndex;

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is ChessSquare other && Equals(other);

	/// <inheritdoc />
	public override int GetHashCode() => HashCode.Combine(FileIndex, RankIndex);

	/// <summary>
	///     Formats the square using algebraic file/rank notation.
	/// </summary>
	public override string ToString() => string.Concat(File.ToString(), Rank.ToString(CultureInfo.InvariantCulture));

	private static bool IsValidIndex(int value) => value is >= 0 and < BoardSize;
}
