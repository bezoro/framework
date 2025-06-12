using System;

namespace ChessLogic
{
	/// <summary>
	///     Represents a position on the chessboard using both 0-indexed row/column
	///     and standard algebraic notation (e.g., "e4").
	/// </summary>
	public readonly struct Position : IEquatable<Position>
	{
		public Position(int row, int col)
		{
			Row = row;
			Col = col;
		}

		/// <summary>
		///     Creates a Position from standard algebraic notation like "e4".
		/// </summary>
		public Position(string algebraicNotation)
		{
			if (string.IsNullOrEmpty(algebraicNotation) || algebraicNotation.Length != 2)
			{
				throw new ArgumentException("Invalid algebraic notation.", nameof(algebraicNotation));
			}

			var file = char.ToLower(algebraicNotation[0]);
			var rank = algebraicNotation[1];

			if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
			{
				throw new ArgumentException(
					$"Invalid algebraic notation: {algebraicNotation}", nameof(algebraicNotation));
			}

			Col = file - 'a';
			Row = 8    - (rank - '0');
		}

		public int Col { get; }
		public int Row { get; }

		/// <summary>
		///     Converts the position to its standard algebraic notation (e.g., "e4").
		/// </summary>
		public override string ToString()
		{
			var file = (char)('a' + Col);
			var rank = (char)('0' + (8 - Row));
			return $"{file}{rank}";
		}

	#region Equality Members

		public bool Equals(Position other) => Row == other.Row           && Col == other.Col;
		public override bool Equals(object obj) => obj is Position other && Equals(other);
		public override int GetHashCode() => HashCode.Combine(Row, Col);
		public static bool operator ==(Position left, Position right) => left.Equals(right);
		public static bool operator !=(Position left, Position right) => !left.Equals(right);

	#endregion
	}
}
