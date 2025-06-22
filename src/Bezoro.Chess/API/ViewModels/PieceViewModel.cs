using System;
using Bezoro.Chess.Domain.Extensions;
using PieceColor = Bezoro.Chess.API.Shared.Enums.PieceColor;
using PieceType = Bezoro.Chess.API.Shared.Enums.PieceType;

namespace Bezoro.Chess.API.ViewModels
{
	/// <summary>
	///     A simple data structure to represent a piece for the View.
	///     It decouples the View from the core Piece class.
	/// </summary>
	public readonly struct PieceViewModel : IEquatable<PieceViewModel>
	{
		public PieceViewModel((PieceType type, PieceColor color) piece)
		{
			Type  = piece.type;
			Color = piece.color;
		}

		internal PieceViewModel((Domain.Shared.Enums.PieceType type, Domain.Shared.Enums.PieceColor color) piece) :
			this((piece.type.ToAPI(), piece.color.ToAPI())) { }

		public PieceColor Color { get; }

		public PieceType Type { get; }

		public static bool operator ==(PieceViewModel left, PieceViewModel right) => left.Equals(right);
		public static bool operator !=(PieceViewModel left, PieceViewModel right) => !left.Equals(right);

		#region Equality

		public bool Equals(PieceViewModel other) => Type == other.Type && Color == other.Color;

		public override bool Equals(object obj) => obj is PieceViewModel other && Equals(other);

		public override int GetHashCode() => HashCode.Combine((int)Type, (int)Color);

		#endregion
	}
}
