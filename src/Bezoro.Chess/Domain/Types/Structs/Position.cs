using System;
using Bezoro.Chess.API.Shared.Enums;
using Bezoro.Chess.Domain.Extensions;

namespace Bezoro.Chess.Domain.Types.Structs
{
	/// <summary>
	///     Represents a position on the chessboard using both 0-indexed row/column
	///     and standard algebraic notation (e.g., "e4").
	/// </summary>
	internal readonly struct Position : IEquatable<Position>
	{
		private readonly bool _initialized;

		public Position(int row, int col)
		{
			Row          = row;
			Col          = col;
			_initialized = true;
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

			char file = char.ToLower(algebraicNotation[0]);
			char rank = algebraicNotation[1];

			if (file < 'a' || file > 'h' || rank < '1' || rank > '8')
			{
				throw new ArgumentException(
					$"Invalid algebraic notation: {algebraicNotation}", nameof(algebraicNotation));
			}

			Col          = file - 'a';
			Row          = rank - '1';
			_initialized = true;
		}

		public bool IsValid => _initialized && Row is >= 0 and < 8 && Col is >= 0 and < 8;

		public ChessSquareCoordinate Coordinate => (Col, Row).ToSquareCoordinate();

		public int Col { get; }
		public int Row { get; }

		public static bool operator ==(Position left, Position right) => left.Equals(right);
		public static bool operator !=(Position left, Position right) => !left.Equals(right);

		#region Equality

		public bool Equals(Position other) => Row == other.Row           && Col == other.Col;
		public override bool Equals(object obj) => obj is Position other && Equals(other);
		public override int GetHashCode() => HashCode.Combine(Row, Col);

		#endregion

		/// <summary>
		///     Converts the position to its standard algebraic notation (e.g., "e4").
		/// </summary>
		public override string ToString()
		{
			var file = (char)('a' + Col);
			var rank = (char)('1' + Row);
			return $"{file}{rank}";
		}
	}
}
