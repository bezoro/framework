using System;
using System.Numerics;
using Bezoro.Core.Chess.Utils;

// Vector2

// Algebraic helpers

namespace Bezoro.Core.Chess
{
	/// <summary>
	///     Immutable value object that represents a square on a rectangular board.
	///     The default board is 8×8, but arbitrary dimensions are supported.
	/// </summary>
	public readonly struct BoardPosition : IEquatable<BoardPosition>
	{
		/// <summary>Create from 0-based file / rank.</summary>
		public BoardPosition(int column, int rank, int maxCol = 8, int maxRow = 8)
		{
			if (maxCol < 1) throw new ArgumentOutOfRangeException(nameof(maxCol));
			if (maxRow < 1) throw new ArgumentOutOfRangeException(nameof(maxRow));

			Column = column;
			Rank   = rank;
			MaxCol = maxCol;
			MaxRow = maxRow;

			Algebraic = IsValid(Column, Rank, MaxCol, MaxRow)
				? $"{(char)('a' + Column)}{Rank + 1}"
				: "Invalid";
		}

		/// <summary>Create from algebraic string (e.g. "e4").</summary>
		public BoardPosition(string algebraic, int maxCol = 8, int maxRow = 8)
			: this(
				AlgebraicNotationUtils.FromAlgebraic(algebraic).Column,
				AlgebraicNotationUtils.FromAlgebraic(algebraic).Rank,
				maxCol, maxRow) { }

		/// <summary>Create from a <see cref="Vector2" /> (X=file, Y=rank).</summary>
		public static BoardPosition FromVector(Vector2 vec, int maxCol = 8, int maxRow = 8) =>
			new((int)vec.X, (int)vec.Y, maxCol, maxRow);

		public static bool operator ==(BoardPosition left, BoardPosition right) => left.Equals(right);
		public static bool operator !=(BoardPosition left, BoardPosition right) => !(left == right);

		/// <summary>“a”.. “h” for 8×8, or generalized up to 26 columns.</summary>
		public char File => (char)('a' + Column);
		public int Column { get; } // 0-based column
		public int MaxCol { get; } // board width
		public int MaxRow { get; } // board height
		public int Rank   { get; } // 0-based row
		public int Row    => Rank;

		/// <summary>Pre-computed algebraic notation or “Invalid”.</summary>
		public string Algebraic { get; }

		/// <summary>Column/row pair as a Vector2 (<c>X=file</c>, <c>Y=rank</c>).</summary>
		public Vector2 Vector => new(Column, Rank);

	#region Interface Implementations

		public bool Equals(BoardPosition other) =>
			Column == other.Column && Rank == other.Rank && MaxCol == other.MaxCol && MaxRow == other.MaxRow;

	#endregion

		public override bool Equals(object? obj) =>
			obj is BoardPosition other && Equals(other);

		public override int GetHashCode() =>
			HashCode.Combine(Column, Rank, MaxCol, MaxRow);

		public override string ToString() => Algebraic;

		/// <returns><c>true</c> when the square lies inside the board.</returns>
		public bool IsValid() =>
			Column >= 0 && Column < MaxCol && Rank >= 0 && Rank < MaxRow;

		/// <summary>Deconstruct into file and rank (both 0-based).</summary>
		public void Deconstruct(out int file, out int rank)
		{
			file = Column;
			rank = Rank;
		}

		private static bool IsValid(int file, int rank, int maxCol, int maxRow) =>
			file >= 0 && file < maxCol && rank >= 0 && rank < maxRow;
	}
}
