// NormalMove.cs

using System;
using Bezoro.Chess.Domain.Abstractions;
using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Types.Structs.Moves
{
	internal readonly struct NormalMove : IMove, IEquatable<NormalMove>
	{
		public NormalMove(Position from, Position to, Piece piece)
		{
			From        = from;
			To          = to;
			MovingPiece = piece;
		}

		public MoveType Kind => MoveType.Normal;

		public Piece    MovingPiece { get; }
		public Position From        { get; }
		public Position To          { get; }

		public static bool operator ==(NormalMove left, NormalMove right) => left.Equals(right);
		public static bool operator !=(NormalMove left, NormalMove right) => !left.Equals(right);

		#region Equality

		public bool Equals(NormalMove other) =>
			Kind.Equals(other.Kind) &&
			From.Equals(other.From) &&
			To.Equals(other.To)     &&
			MovingPiece.Equals(other.MovingPiece);

		public override bool Equals(object? obj) =>
			obj is NormalMove other && Equals(other);

		public override int GetHashCode() =>
			(Kind, From, To, MovingPiece).GetHashCode();

		#endregion

		public override string ToString() => this.ToBaseAlgebraicNotation();
	}
}
