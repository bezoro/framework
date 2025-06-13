using System;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Application.Abstractions.ViewModels
{
	/// <summary>
	///     A simple data structure to represent a piece for the View.
	///     It decouples the View from the core Piece class.
	/// </summary>
	public readonly struct PieceViewModel : IEquatable<PieceViewModel>
	{
		public PieceViewModel(PieceType type, PieceColor color)
		{
			Type  = type;
			Color = color;
		}

		public PieceColor Color { get; }

		public PieceType Type { get; }

	#region Equality

		public bool Equals(PieceViewModel other) => Type == other.Type && Color == other.Color;

		public override bool Equals(object obj) => obj is PieceViewModel other && Equals(other);

		public override int GetHashCode() => HashCode.Combine((int)Type, (int)Color);

		public static bool operator ==(PieceViewModel left, PieceViewModel right) => left.Equals(right);

		public static bool operator !=(PieceViewModel left, PieceViewModel right) => !left.Equals(right);

	#endregion
	}
}
