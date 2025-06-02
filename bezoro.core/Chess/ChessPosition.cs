using System;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	/// <summary>
	///     Represents a position on the chess board using file (column) and rank (row)
	/// </summary>
	public readonly struct ChessPosition : IEquatable<ChessPosition>
	{
		public ChessPosition(int file, int rank, int maxRow = 8, int maxCol = 8)
		{
			File   = file;
			Rank   = rank;
			MaxRow = maxRow;
			MaxCol = maxCol;
		}

		public ChessPosition(string algebraic, int maxRow = 8, int maxCol = 8)
		{
			var position = AlgebraicNotationUtils.FromAlgebraic(algebraic);
			File   = position.File;
			Rank   = position.Rank;
			MaxRow = maxRow;
			MaxCol = maxCol;
		}

		// Invalid position constant
		public static readonly ChessPosition INVALID = new(-1, -1);

		public static bool operator ==(ChessPosition left, ChessPosition right) =>
			left.Equals(right);

		public static bool operator !=(ChessPosition left, ChessPosition right) =>
			!left.Equals(right);

		public string Algebraic => AlgebraicNotationUtils.ToAlgebraic(File, Rank);

		/// <summary>
		///     Column (0-7, representing a-h)
		/// </summary>
		public int File { get; }
		public int MaxCol { get; }
		public int MaxRow { get; }

		/// <summary>
		///     Row (0-7, representing 1-8)
		/// </summary>
		public int Rank { get; }

	#region Interface Implementations

		public bool Equals(ChessPosition other) =>
			File == other.File && Rank == other.Rank;

	#endregion

		public override bool Equals(object obj) =>
			obj is ChessPosition other && Equals(other);

		public override int GetHashCode() =>
			HashCode.Combine(File, Rank);

		/// <summary>
		///     Convert to chess notation (e.g., "e4")
		/// </summary>
		public override string ToString() => !IsValid() ? "Invalid" : $"{(char)('a' + File)}{Rank + 1}";

		/// <summary>
		///     Returns true if this position is within the chess board bounds
		/// </summary>
		/// <summary>
		///     Returns true if this position is within the chess board bounds
		/// </summary>
		public bool IsValid() => File >= 0 && File < MaxCol && Rank >= 0 && Rank < MaxRow;
	}
}
