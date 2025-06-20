using System;
using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Types.Structs
{
	/// <summary>
	///     Enumerates the types of possible moves in chess.
	/// </summary>
	internal enum MoveType : byte
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
	internal readonly struct Move : IEquatable<Move>
	{
		private Move(
			Position from, Position to, Piece piece, Piece capturedPiece = default, MoveType type = MoveType.Normal,
			PromotionType promotionPieceType = PromotionType.None)
		{
			From               = from;
			To                 = to;
			Piece              = piece;
			CapturedPiece      = capturedPiece;
			Type               = type;
			PromotionPieceType = promotionPieceType;
		}

		public bool IsCapture   => CapturedPiece.Type != PieceType.None && Type == MoveType.Capture;
		public bool IsCastle    => Type is MoveType.CastleKingside or MoveType.CastleQueenside;
		public bool IsEnPassant => Type == MoveType.EnPassant;
		public bool IsPromotion => PromotionPieceType != PromotionType.None && Type == MoveType.PawnPromotion;
		public bool IsQuiet     => Type == MoveType.Normal;
		public bool IsValid     => Type != MoveType.None;

		public MoveType      Type               { get; }
		public Piece         CapturedPiece      { get; }
		public Piece         Piece              { get; } // TODO: refactor to piece type for a smaller struct
		public Position      From               { get; }
		public Position      To                 { get; }
		public PromotionType PromotionPieceType { get; }

		public static bool operator ==(Move left, Move right) => left.Equals(right);

		public static bool operator !=(Move left, Move right) => !left.Equals(right);

		#region Equality

		public bool Equals(Move other) =>
			Type == other.Type                        &&
			Piece.Equals(other.Piece)                 &&
			CapturedPiece.Equals(other.CapturedPiece) &&
			From.Equals(other.From)                   &&
			To.Equals(other.To)                       &&
			PromotionPieceType == other.PromotionPieceType;

		public override bool Equals(object? obj) => obj is Move other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(
			(int)Type,
			Piece,
			CapturedPiece,
			From,
			To,
			(int)PromotionPieceType
		);

		#endregion

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

		public static Move CreateQuietPromotion(Position from, Position to, Piece pawn, PromotionType promotionPiece) =>
			new(from, to, pawn, type: MoveType.PawnPromotion, promotionPieceType: promotionPiece);

		public static Move CreateCapturePromotion(
			Position from, Position to, Piece pawn, Piece capturedPiece, PromotionType promotionPiece) =>
			new(from, to, pawn, capturedPiece, MoveType.PawnPromotionCapture, promotionPiece);

		#endregion
	}
}
