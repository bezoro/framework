using System;
using Bezoro.Chess.Domain.Abstractions;
using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Types.Structs.Moves
{
	internal readonly struct EnPassantMove : IEnPassant, IEquatable<EnPassantMove>
	{
		public EnPassantMove(Position from, Position to, Piece movingPawn, Piece capturedPawn)
		{
			From          = from;
			To            = to;
			CapturedPiece = capturedPawn;
			MovingPiece   = movingPawn;
		}

		public MoveType Kind          => MoveType.EnPassant;
		public Piece    CapturedPiece { get; }
		public Piece    MovingPiece   { get; }
		public Position From          { get; }
		public Position To            { get; }

		public static bool operator ==(EnPassantMove left, EnPassantMove right) => left.Equals(right);
		public static bool operator !=(EnPassantMove left, EnPassantMove right) => !left.Equals(right);

		#region Equality

		public bool Equals(EnPassantMove other) =>
			From.Equals(other.From)               &&
			To.Equals(other.To)                   &&
			MovingPiece.Equals(other.MovingPiece) &&
			CapturedPiece.Equals(other.CapturedPiece);

		public override bool Equals(object? obj) =>
			obj is EnPassantMove other && Equals(other);

		public override int GetHashCode() =>
			HashCode.Combine(From, To, MovingPiece, CapturedPiece);

		#endregion
	}
}
