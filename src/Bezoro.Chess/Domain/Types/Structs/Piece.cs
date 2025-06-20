using System;
using System.Runtime.CompilerServices;
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(in Piece left, in Piece right) =>
			left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(in Piece left, in Piece right) =>
			!left.Equals(right);

		#region Equality

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Piece other) =>
			Type == other.Type && Color == other.Color;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object? obj) =>
			obj is Piece other && Equals(other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() =>
			HashCode.Combine((int)Type, (int)Color);

		#endregion

		/// <summary>
		///     Returns a string representation of the piece, primarily for debugging.
		/// </summary>
		public override string ToString() =>
			Type == PieceType.None ? "Empty" : $"{Color} {Type}";
	}
}
