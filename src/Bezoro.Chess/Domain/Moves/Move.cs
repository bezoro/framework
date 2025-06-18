using System;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves
{
	/// <summary>
	///     Enumerates the types of possible moves in chess.
	/// </summary>
	public enum MoveType : byte
	{
		None,
		Normal,
		Capture,
		CastleKingside,
		CastleQueenside,
		EnPassant,
		PawnPromotion,
		PawnPromotionCapture
	}

	/// <summary>
	///     Represents a single move from a starting position to an end position.
	/// </summary>
	public readonly struct Move : IEquatable<Move>
	{
		public MoveType  Type           { get; }
		public Piece     CapturedPiece  { get; }
		public Piece     Piece          { get; }
		public PieceType PromotionPiece { get; }
		public Position  From           { get; }
		public Position  To             { get; }

		#region Equality

		public static bool operator ==(Move left, Move right) => left.Equals(right);

		public static bool operator !=(Move left, Move right) => !left.Equals(right);

		public bool Equals(Move other) =>
			Type == other.Type                        &&
			Piece.Equals(other.Piece)                 &&
			CapturedPiece.Equals(other.CapturedPiece) &&
			From.Equals(other.From)                   &&
			To.Equals(other.To)                       &&
			PromotionPiece == other.PromotionPiece;

		public override bool Equals(object? obj) => obj is Move other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(
			(int)Type,
			Piece,
			CapturedPiece,
			From,
			To,
			(int)PromotionPiece
		);

		#endregion

		private Move(
			Position from, Position to, Piece piece, Piece capturedPiece = default, MoveType type = MoveType.Normal,
			PieceType promotionPiece = PieceType.None)
		{
			From           = from;
			To             = to;
			Piece          = piece;
			CapturedPiece  = capturedPiece;
			Type           = type;
			PromotionPiece = promotionPiece;
		}

		public override string ToString() => $"Move {From} -> {To} ({Type})";

		#region Factory Methods

		public static Move CreateNormal(Position from, Position to, Piece piece) =>
			new(from, to, piece, type: MoveType.Normal);

		public static Move CreateCapture(Position from, Position to, Piece piece, Piece capturedPiece) =>
			new(from, to, piece, capturedPiece, MoveType.Capture);

		public static Move CreateCastleKingside(Position from, Position to, Piece king) =>
			new(from, to, king, type: MoveType.CastleKingside);

		public static Move CreateCastleQueenside(Position from, Position to, Piece king) =>
			new(from, to, king, type: MoveType.CastleQueenside);

		public static Move CreateEnPassant(Position from, Position to, Piece pawn, Piece capturedPawn) =>
			new(from, to, pawn, capturedPawn, MoveType.EnPassant);

		public static Move CreateQuietPromotion(Position from, Position to, Piece pawn, PieceType promotionPiece) =>
			new(from, to, pawn, type: MoveType.PawnPromotion, promotionPiece: promotionPiece);

		public static Move CreateCapturePromotion(
			Position from, Position to, Piece pawn, Piece capturedPiece, PieceType promotionPiece) =>
			new(from, to, pawn, capturedPiece, MoveType.PawnPromotionCapture, promotionPiece);

		#endregion
	}
}
