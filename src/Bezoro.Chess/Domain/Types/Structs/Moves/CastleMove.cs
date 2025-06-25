using System;
using Bezoro.Chess.Domain.Abstractions;
using Bezoro.Chess.Domain.Functions.Moves.Generation;
using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Types.Structs.Moves
{
	internal readonly struct CastleMove : ICastle, IEquatable<CastleMove>
	{
		public CastleMove(PieceColor color, CastlingSide side)
		{
			string rank = color == PieceColor.White ? "1" : "8";

			Side        = side;
			From        = new Position($"e{rank}");
			To          = new Position($"{(side == CastlingSide.Kingside ? 'g' : 'c')}{rank}");
			RookFrom    = new Position($"{(side == CastlingSide.Kingside ? 'h' : 'a')}{rank}");
			RookTo      = new Position($"{(side == CastlingSide.Kingside ? 'f' : 'd')}{rank}");
			MovingPiece = new Piece(PieceType.King, color);
		}

		public CastlingSide Side        { get; }
		public MoveType     Kind        => MoveType.Castling;
		public Piece        MovingPiece { get; }
		public Position     From        { get; }
		public Position     RookFrom    { get; }
		public Position     RookTo      { get; }
		public Position     To          { get; }

		public static bool operator ==(CastleMove left, CastleMove right) => left.Equals(right);
		public static bool operator !=(CastleMove left, CastleMove right) => !left.Equals(right);

		#region Equality

		public bool Equals(CastleMove other) =>
			From.Equals(other.From)               &&
			To.Equals(other.To)                   &&
			RookFrom.Equals(other.RookFrom)       &&
			RookTo.Equals(other.RookTo)           &&
			MovingPiece.Equals(other.MovingPiece) &&
			Side == other.Side                    &&
			Kind == other.Kind;

		public override bool Equals(object? obj) =>
			obj is CastleMove other && Equals(other);

		public override int GetHashCode() =>
			(From, To, RookFrom, RookTo, MovingPiece, Side, Kind).GetHashCode();

		#endregion

		public override string ToString() => Side == CastlingSide.Kingside ? "O-O" : "O-O-O";
	}
}
