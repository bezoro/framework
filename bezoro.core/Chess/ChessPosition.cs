using System;

namespace Bezoro.Core.Chess
{
	/// <summary>
	///     Represents a position on the chess board using file (column) and rank (row)
	/// </summary>
	public readonly struct ChessPosition : IEquatable<ChessPosition>
	{
		public ChessPosition(int file, int rank)
		{
			File = file;
			Rank = rank;
		}

		public ChessPosition(char file, int rank)
		{
			File = file - 'a'; // Convert 'a'-'h' to 0-7
			Rank = rank - 1;   // Convert 1-8 to 0-7
		}

		// Invalid position constant
		public static readonly ChessPosition INVALID = new(-1, -1);

		public static bool operator ==(ChessPosition left, ChessPosition right) =>
			left.Equals(right);

		public static bool operator !=(ChessPosition left, ChessPosition right) =>
			!left.Equals(right);

		/// <summary>
		///     Column (0-7, representing a-h)
		/// </summary>
		public int File { get; }

		/// <summary>
		///     Returns true if this position is within the chess board bounds
		/// </summary>
		public bool IsValid => File is >= 0 and < 8 && Rank is >= 0 and < 8;

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
		public override string ToString() =>
			!IsValid ? "Invalid" : $"{(char)('a' + File)}{Rank + 1}";
	}
}
