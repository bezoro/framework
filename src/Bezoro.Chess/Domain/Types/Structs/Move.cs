using System;
using Bezoro.Chess.Domain.Functions.Moves.Generation;
using Bezoro.Chess.Domain.Shared.Enums;

namespace Bezoro.Chess.Domain.Types.Structs
{
	/// <summary>
	///     Immutable value that represents one legal chess move.
	///     Use the static factories to create instances and <see cref="Match{T}" /> to
	///     perform variant-specific logic without casts or allocations.
	/// </summary>
	internal readonly struct Move : IEquatable<Move>
	{
		private Move(
			MoveType kind,
			Position from,
			Position to,
			Piece piece,
			Piece capturedPiece = default,
			PromotionType promotionPieceType = PromotionType.None)
		{
			Type               = kind;
			From               = from;
			To                 = to;
			Piece              = piece;
			CapturedPiece      = capturedPiece;
			PromotionPieceType = promotionPieceType;
		}

		private Move(MoveType type, PieceColor color, CastlingSide side)
		{
			Type               = type;
			From               = default;
			To                 = default;
			Piece              = new Piece(PieceType.King, color);
			CapturedPiece      = default;
			PromotionPieceType = PromotionType.None;
		}

		public bool IsCapture   => Type is MoveType.Capture or MoveType.PromotionCapture;
		public bool IsCastle    => Type is MoveType.Castling;
		public bool IsEnPassant => Type == MoveType.EnPassant;
		public bool IsPromotion => Type is MoveType.Promotion or MoveType.PromotionCapture;
		public bool IsQuiet     => Type == MoveType.Normal;

		public bool          IsValid            => Type != MoveType.None;
		public MoveType      Type               { get; }
		public Piece         CapturedPiece      { get; } // only valid for captures
		public Piece         Piece              { get; }
		public Position      From               { get; }
		public Position      To                 { get; }
		public PromotionType PromotionPieceType { get; } // only valid for promotions

		public static bool operator ==(Move left, Move right) => left.Equals(right);
		public static bool operator !=(Move left, Move right) => !left.Equals(right);

		#region Equality

		public bool Equals(Move other) =>
			(From, To, Piece, CapturedPiece, PromotionPieceType, Type) ==
			(other.From, other.To, other.Piece, other.CapturedPiece, other.PromotionPieceType, other.Type);

		public override bool Equals(object? obj) => obj is Move m && Equals(m);

		public override int GetHashCode() =>
			(From, To, Piece, CapturedPiece, PromotionPieceType, MoveKind: Type).GetHashCode();

		#endregion

		public static Move Capture(Position from, Position to, Piece piece, Piece captured) =>
			new(MoveType.Capture, from, to, piece, captured);

		public static Move Castling(PieceColor color, CastlingSide side) =>
			new(MoveType.Castling, color, side);

		public static Move EnPassant(Position from, Position to, Piece pawn, Piece capturedPawn) =>
			new(MoveType.EnPassant, from, to, pawn, capturedPawn);

		public static Move Normal(Position from, Position to, Piece piece) =>
			new(MoveType.Normal, from, to, piece);

		public static Move Promotion(Position from, Position to, Piece pawn, PromotionType promoteTo) =>
			new(MoveType.Promotion, from, to, pawn, promotionPieceType: promoteTo);

		public static Move PromotionCapture(
			Position from,
			Position to,
			Piece pawn,
			Piece captured,
			PromotionType promoteTo) =>
			new(MoveType.PromotionCapture, from, to, pawn, captured, promoteTo);

		public override string ToString() => $"{Type}: {From}->{To}";

		public T Match<T>(
			Func<Move, T> onNormal,
			Func<Move, T> onCapture,
			Func<Move, T> onCastle,
			Func<Move, T> onEnPassant,
			Func<Move, T> onPromotion,
			Func<Move, T> onPromotionCapture,
			Func<T>? onNone = null)
		{
			return Type switch
			{
				MoveType.Normal           => onNormal(this),
				MoveType.Capture          => onCapture(this),
				MoveType.Castling         => onCastle(this),
				MoveType.EnPassant        => onEnPassant(this),
				MoveType.Promotion        => onPromotion(this),
				MoveType.PromotionCapture => onPromotionCapture(this),
				_ => onNone is null
					? throw new InvalidOperationException("Move is None")
					: onNone()
			};
		}

		public void Deconstruct(out Position from, out Position to, out Piece piece, out MoveType kind)
		{
			from  = From;
			to    = To;
			piece = Piece;
			kind  = Type;
		}
	}
}
