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
		private readonly int  _col;
		private readonly int  _row;

		public Position(int row, int col)
		{
			_row         = row;
			_col         = col;
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

			_col         = file - 'a';
			_row         = rank - '1';
			_initialized = true;
		}

		public bool IsValid => _initialized && _row is >= 0 and < 8 && _col is >= 0 and < 8;

		public ChessSquareCoordinate Coordinate => (_col, _row).ToSquareCoordinate();

		public int Col
		{
			get
			{
				EnsureInitialized();
				return _col;
			}
		}

		public int Row
		{
			get
			{
				EnsureInitialized();
				return _row;
			}
		}

		public static bool operator ==(Position left, Position right) => left.Equals(right);
		public static bool operator !=(Position left, Position right) => !left.Equals(right);

		#region Equality

		public bool Equals(Position other) =>
			_row         == other._row &&
			_col         == other._col &&
			_initialized == other._initialized;

		public override bool Equals(object? obj) => obj is Position other && Equals(other);
		public override int GetHashCode() => HashCode.Combine(_row, _col, _initialized);

		#endregion

		/// <summary>
		///     Converts the position to its standard algebraic notation (e.g., "e4").
		/// </summary>
		public override string ToString() => IsValid
			? $"{(char)('a' + _col)}{(char)('1' + _row)}"
			: "<invalid>";

		private void EnsureInitialized()
		{
			if (!_initialized)
			{
				throw new InvalidOperationException(
					"Attempted to use a default / uninitialised Position instance.");
			}
		}
	}
}
