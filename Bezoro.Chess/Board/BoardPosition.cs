using System;
using Bezoro.Chess.Common.Helpers;

namespace Bezoro.Chess.Board
{
	/// <summary>
	///     Immutable value object that identifies a square by its zero-based
	///     column (file) and row (rank). Represents a position on a chess board
	///     using zero-based indices for internal storage while providing chess-standard
	///     algebraic notation (e.g., "e4") for external representation.
	/// </summary>
	/// <summary>
	///     Immutable value object that identifies a square by its zero-based
	///     column (file) and row (rank).
	/// </summary>
	public readonly struct BoardPosition : IEquatable<BoardPosition>
	{
		/// <summary>Creates a position from zero-based indices representing a square on the chess board.</summary>
		/// <param name="column">Zero-based column index (0 = file "a", 1 = file "b", etc.)</param>
		/// <param name="row">Zero-based row index (0 = rank "1", 1 = rank "2", etc.)</param>
		/// <exception cref="ArgumentOutOfRangeException">
		///     Thrown when <paramref name="column" /> or <paramref name="row" /> is negative.
		/// </exception>
		public BoardPosition(uint column, uint row)
		{
			Column = column;
			Row    = row;
		}

		public BoardPosition(int column, int row) : this((uint)column, (uint)row) { }

		/// <summary>“a1” on every board.</summary>
		public static readonly BoardPosition A1 = new(0, 0);

		/// <summary>Determines whether two board positions are equal.</summary>
		/// <param name="left">The first position to compare.</param>
		/// <param name="right">The second position to compare.</param>
		/// <returns>true if the positions are equal; otherwise, false.</returns>
		public static bool operator ==(BoardPosition left, BoardPosition right) => left.Equals(right);

		/// <summary>Determines whether two board positions are not equal.</summary>
		/// <param name="left">The first position to compare.</param>
		/// <param name="right">The second position to compare.</param>
		/// <returns>true if the positions are not equal; otherwise, false.</returns>
		public static bool operator !=(BoardPosition left, BoardPosition right) => !left.Equals(right);

		/// <summary>User-friendly algebraic form (e.g. “e4”).</summary>
		public string Algebraic => AlgebraicNotationUtils.ToAlgebraic(Column, Row);
		/// <summary>
		///     File token (e.g. “a”, “h”, “aa”) derived from <see cref="Column" />.
		/// </summary>
		public string File => AlgebraicNotationUtils.IndexToFileToken(Column);

		/// <summary>Column index, 0 = file “a”.</summary>
		public uint Column { get; }

		/// <summary>
		///     1-based rank derived from <see cref="Row" />. Example: row 0 → rank 1.
		/// </summary>
		public uint Rank => Row + 1;

		/// <summary>Row index, 0 = rank “1”.</summary>
		public uint Row { get; }

	#region Interface Implementations

		/// <summary>
		///     Determines whether the specified position is equal to the current position.
		///     Two positions are considered equal if they have the same column and row values.
		/// </summary>
		/// <param name="other">The position to compare with the current position.</param>
		/// <returns>true if the specified position is equal to the current position; otherwise, false.</returns>
		public bool Equals(BoardPosition other) => Column == other.Column && Row == other.Row;

	#endregion

		public override bool Equals(object? obj) => obj is BoardPosition other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(Column, Row);

		public override string ToString() => Algebraic;
	}
}
