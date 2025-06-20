using System;
using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Types.Structs
{
	/// <summary>
	///     Represents a chess piece. This is a struct for memory efficiency when storing the board state.
	/// </summary>
	internal readonly struct Piece : IEquatable<Piece>
	{
		public Piece(PieceType type, PieceColor color)
		{
			Type  = type;
			Color = color;
		}

		/// <summary>
		///     The color of the piece (White or Black)
		/// </summary>
		public PieceColor Color { get; }
		/// <summary>
		///     The type of piece (Pawn, Knight, etc.)
		/// </summary>
		public PieceType Type { get; }

		public static bool operator ==(Piece left, Piece right) => left.Equals(right);

		public static bool operator !=(Piece left, Piece right) => !left.Equals(right);

		#region Equality

		public bool Equals(Piece other) => Type == other.Type && Color == other.Color;

		public override bool Equals(object obj) => obj is Piece other && Equals(other);

		public override int GetHashCode() => HashCode.Combine((int)Type, (int)Color);

		#endregion

		/// <summary>
		///     Returns a string representation of the piece, primarily for debugging.
		/// </summary>
		public override string ToString() =>
			Type == PieceType.None ? "Empty" : $"{Color} {Type}";
	}
}
