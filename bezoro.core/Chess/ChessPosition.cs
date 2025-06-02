using System;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	/// <summary>
	///     Represents a position on the chess board using file (column) and rank (row)
	/// </summary>
	public readonly struct ChessPosition : IEquatable<ChessPosition>
	{
		/// <summary>
		///     Initializes a new ChessPosition with specified file and rank
		/// </summary>
		/// <param name="file">Column (0-based, representing a-h)</param>
		/// <param name="rank">Row (0-based, representing 1-8)</param>
		/// <param name="maxCol">Maximum number of columns on the board</param>
		/// <param name="maxRow">Maximum number of rows on the board</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when maxCol or maxRow are less than 1</exception>
		public ChessPosition(int file, int rank, int maxCol = 8, int maxRow = 8)
		{
			if (maxCol < 1) throw new ArgumentOutOfRangeException(nameof(maxCol), "Board must have at least 1 column");
			if (maxRow < 1) throw new ArgumentOutOfRangeException(nameof(maxRow), "Board must have at least 1 row");
			
			File = file;
			Rank = rank;
			MaxRow = maxRow;
			MaxCol = maxCol;
		}

		/// <summary>
		///     Initializes a new ChessPosition from algebraic notation
		/// </summary>
		/// <param name="algebraic">Algebraic notation (e.g., "e4")</param>
		/// <param name="maxCol">Maximum number of columns on the board</param>
		/// <param name="maxRow">Maximum number of rows on the board</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when maxCol or maxRow are less than 1</exception>
		public ChessPosition(string algebraic, int maxCol = 8, int maxRow = 8)
		{
			if (maxCol < 1) throw new ArgumentOutOfRangeException(nameof(maxCol), "Board must have at least 1 column");
			if (maxRow < 1) throw new ArgumentOutOfRangeException(nameof(maxRow), "Board must have at least 1 row");
			
			var position = AlgebraicNotationUtils.FromAlgebraic(algebraic);
			File = position.File;
			Rank = position.Rank;
			MaxRow = maxRow;
			MaxCol = maxCol;
		}

		public static bool operator ==(ChessPosition left, ChessPosition right) =>
			left.Equals(right);

		public static bool operator !=(ChessPosition left, ChessPosition right) =>
			!left.Equals(right);

		/// <summary>
		///     Gets the algebraic notation representation of this position
		/// </summary>
		public string Algebraic => AlgebraicNotationUtils.ToAlgebraic(File, Rank);

		/// <summary>
		///     Column (0-based, representing a-h)
		/// </summary>
		public int File { get; }
		
		/// <summary>
		///     Maximum number of columns on the board
		/// </summary>
		public int MaxCol { get; }
		
		/// <summary>
		///     Maximum number of rows on the board
		/// </summary>
		public int MaxRow { get; }

		/// <summary>
		///     Row (0-based, representing 1-8)
		/// </summary>
		public int Rank { get; }

	#region Interface Implementations

		/// <summary>
		///     Determines whether this position is equal to another position
		/// </summary>
		/// <param name="other">The position to compare with</param>
		/// <returns>True if positions are equal (same file, rank, and board dimensions)</returns>
		public bool Equals(ChessPosition other) =>
			File == other.File && Rank == other.Rank && MaxCol == other.MaxCol && MaxRow == other.MaxRow;

	#endregion

		/// <summary>
		///     Determines whether this position is equal to the specified object
		/// </summary>
		/// <param name="obj">The object to compare with</param>
		/// <returns>True if the object is a ChessPosition and equals this position</returns>
		public override bool Equals(object obj) =>
			obj is ChessPosition other && Equals(other);

		/// <summary>
		///     Returns the hash code for this position
		/// </summary>
		/// <returns>A hash code combining all position properties</returns>
		public override int GetHashCode() =>
			HashCode.Combine(File, Rank, MaxCol, MaxRow);

		/// <summary>
		///     Convert to chess notation (e.g., "e4") or "Invalid" for invalid positions
		/// </summary>
		/// <returns>String representation of the position</returns>
		public override string ToString()
		{
			if (!IsValid())
				return "Invalid";
			
			// Additional safety check for character conversion
			if (File < 0 || File >= 26)
				return "Invalid";
				
			return $"{(char)('a' + File)}{Rank + 1}";
		}

		/// <summary>
		///     Returns true if this position is within the chess board bounds
		/// </summary>
		/// <returns>True if the position is valid for the current board dimensions</returns>
		public bool IsValid() => File >= 0 && File < MaxCol && Rank >= 0 && Rank < MaxRow;
	}
}