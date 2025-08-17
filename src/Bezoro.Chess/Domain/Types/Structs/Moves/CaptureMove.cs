using System;
using Bezoro.Chess.Domain.Abstractions;
using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Types.Structs.Moves
{
	internal readonly struct CaptureMove : ICaptureMove, IEquatable<CaptureMove>
	{
		public CaptureMove(Position from, Position to, Piece piece, Piece captured)
		{
			From          = from;
			To            = to;
			CapturedPiece = captured;
			MovingPiece   = piece;
		}

		public MoveType Kind          => MoveType.Capture;
		public Piece    CapturedPiece { get; }
		public Piece    MovingPiece   { get; }
		public Position From          { get; }
		public Position To            { get; }

		public static bool operator ==(CaptureMove left, CaptureMove right) => left.Equals(right);
		public static bool operator !=(CaptureMove left, CaptureMove right) => !left.Equals(right);

		#region Equality

		public bool Equals(CaptureMove other) =>
			Kind.Equals(other.Kind)                   &&
			CapturedPiece.Equals(other.CapturedPiece) &&
			From.Equals(other.From)                   &&
			To.Equals(other.To)                       &&
			MovingPiece.Equals(other.MovingPiece);

		public override bool Equals(object? obj) =>
			obj is CaptureMove other && Equals(other);

		public override int GetHashCode() =>
			(Kind, From, To, MovingPiece, CapturedPiece).GetHashCode();

		#endregion
	}
}
