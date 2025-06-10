using System;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;

namespace Bezoro.Chess.Moves.Models
{
	/// <summary>
	///     Immutable, lightweight value type that represents one <em>possible</em> move
	///     produced by the move-generator.
	///     It is pure data – it does not modify the board and has no side effects.
	/// </summary>
	public readonly struct Move : IEquatable<Move>
	{
		// Private constructor to enforce creation via factory methods
		private Move(
			BoardPosition from,
			BoardPosition to,
			PlayerColor movingSide,
			ChessPieceType pieceType,
			MoveKind kind,
			PromotionPieceType promoteTo,
			CastleSide castleSide,
			bool leavesKingInCheck)
		{
			From              = from;
			To                = to;
			MovingSide        = movingSide;
			PieceType         = pieceType;
			Kind              = kind;
			LeavesKingInCheck = leavesKingInCheck;

			// Initialize and validate type-specific properties
			switch (kind)
			{
				case MoveKind.PromotionQuiet:
				case MoveKind.PromotionCapture:
					if (promoteTo == PromotionPieceType.None)
						throw new ArgumentException(
							"A promotion move must specify the piece to promote to.", nameof(promoteTo));

					if (pieceType != ChessPieceType.Pawn)
						throw new ArgumentException(
							"Promotion moves must have a PieceType of Pawn.", nameof(pieceType));

					PromoteTo  = promoteTo;
					CastleSide = CastleSide.None;
					break;

				case MoveKind.Castle:
					if (pieceType != ChessPieceType.King)
						throw new ArgumentException("Castling moves must have a PieceType of King.", nameof(pieceType));

					if (castleSide == CastleSide.None)
						throw new ArgumentException("Castling moves must specify a CastleSide.", nameof(castleSide));

					PromoteTo  = PromotionPieceType.None;
					CastleSide = castleSide;
					break;

				default: // For Normal, Capture, EnPassant, etc.
					if (promoteTo != PromotionPieceType.None)
						throw new ArgumentException(
							"PromoteTo may only be supplied for non-promotion moves if it is PromotionPieceType.None.",
							nameof(promoteTo));

					if (castleSide != CastleSide.None)
						throw new ArgumentException(
							"CastleSide may only be supplied for non-castling moves if it is CastleSide.None.",
							nameof(castleSide));

					PromoteTo  = PromotionPieceType.None;
					CastleSide = CastleSide.None;
					break;
			}
		}

		/// <summary>
		///     Creates a new <see cref="Move" /> instance with all fields explicitly provided.
		///     This method is intended for scenarios like deserialization or cloning where the full state
		///     of a move is already known. It still enforces the validation logic within the private constructor.
		///     For creating standard moves, promotions, or castles, prefer the more specific factory methods
		///     like <see cref="Standard" />, <see cref="PromotionQuiet" />, or <see cref="CastleKingSide" />.
		/// </summary>
		public static Move Create(
			BoardPosition from,
			BoardPosition to,
			PlayerColor movingSide,
			ChessPieceType pieceType,
			MoveKind kind,
			PromotionPieceType promoteTo,
			CastleSide castleSide,
			bool leavesKingInCheck = false) =>
			new(from, to, movingSide, pieceType, kind, promoteTo, castleSide, leavesKingInCheck);

		public static bool operator ==(Move left, Move right) => left.Equals(right);
		public static bool operator !=(Move left, Move right) => !left.Equals(right);

		public static Move CastleKingSide(
			BoardPosition kingFrom,
			BoardPosition kingTo,
			PlayerColor movingSide,
			bool leavesKingInCheck = false) =>
			new(
				kingFrom, kingTo, movingSide, ChessPieceType.King, MoveKind.Castle, PromotionPieceType.None,
				CastleSide.King, leavesKingInCheck);

		public static Move CastleQueenSide(
			BoardPosition kingFrom,
			BoardPosition kingTo,
			PlayerColor movingSide,
			bool leavesKingInCheck = false) =>
			new(
				kingFrom, kingTo, movingSide, ChessPieceType.King, MoveKind.Castle, PromotionPieceType.None,
				CastleSide.Queen, leavesKingInCheck);

		public static Move PromotionCapture(
			BoardPosition from,
			BoardPosition to,
			PlayerColor movingSide,
			PromotionPieceType promoteTo,
			bool leavesKingInCheck = false) =>
			new(
				from, to, movingSide, ChessPieceType.Pawn, MoveKind.PromotionCapture, promoteTo, CastleSide.None,
				leavesKingInCheck);

		public static Move PromotionQuiet(
			BoardPosition from,
			BoardPosition to,
			PlayerColor movingSide,
			PromotionPieceType promoteTo,
			bool leavesKingInCheck = false) =>
			new(
				from, to, movingSide, ChessPieceType.Pawn, MoveKind.PromotionQuiet, promoteTo, CastleSide.None,
				leavesKingInCheck);

		/// <summary>
		///     Creates a standard move (e.g., normal piece move, capture, en passant).
		///     This factory should not be used for Castling or Promotion moves, which have dedicated factories.
		/// </summary>
		public static Move Standard(
			BoardPosition from,
			BoardPosition to,
			PlayerColor movingSide,
			ChessPieceType pieceType,
			MoveKind kind,
			bool leavesKingInCheck = false)
		{
			if (kind is MoveKind.Castle or MoveKind.PromotionQuiet or MoveKind.PromotionCapture)
			{
				throw new ArgumentException(
					$"Invalid MoveKind '{kind}'. Use dedicated factory methods for Castle or Promotion moves.",
					nameof(kind));
			}

			if (kind == MoveKind.EnPassant && pieceType != ChessPieceType.Pawn)
			{
				throw new ArgumentException("En Passant moves must be made by a Pawn.", nameof(pieceType));
			}

			return new(
				from, to, movingSide, pieceType, kind, PromotionPieceType.None, CastleSide.None, leavesKingInCheck);
		}

		/// <summary>
		///     Position the piece moves from.
		/// </summary>
		public BoardPosition From { get; }

		/// <summary>
		///     Position the piece moves to.
		/// </summary>
		public BoardPosition To { get; }

		public bool IsPromotion => Kind is MoveKind.PromotionQuiet or MoveKind.PromotionCapture;

		/// <summary>
		///     True if the move leaves the king in check.
		/// </summary>
		public bool LeavesKingInCheck { get; }

		/// <summary>
		///     True if the move is a castling move.
		/// </summary>
		public CastleSide CastleSide { get; }

		/// <summary>
		///     The type of piece that is moved.
		/// </summary>
		public ChessPieceType PieceType { get; }

		/// <summary>
		///     Flags describing the kind of move (normal, capture, castling, promotion, …).
		/// </summary>
		public MoveKind Kind { get; }

		/// <summary>
		///     Target piece type when <see cref="Kind" /> is <see cref="MoveKind.PromotionQuiet" /> or
		///     <see cref="MoveKind.PromotionCapture" />.
		///     <see cref="PromotionPieceType.None" /> otherwise.
		/// </summary>
		public PromotionPieceType PromoteTo { get; }

		/// <remarks>
		///     This is intentionally left as the default value; callers may set it through an
		///     object-initializer if they need to tag the move with the side that generated it.
		/// </remarks>
		public PlayerColor MovingSide { get; init; }

	#region Interface Implementations

		public bool Equals(Move other) =>
			From.Equals(other.From)               &&
			To.Equals(other.To)                   &&
			Kind              == other.Kind       &&
			PromoteTo         == other.PromoteTo  &&
			PieceType         == other.PieceType  &&
			MovingSide        == other.MovingSide &&
			CastleSide        == other.CastleSide &&
			LeavesKingInCheck == other.LeavesKingInCheck;

	#endregion

		public override bool Equals(object? obj) =>
			obj is Move m && Equals(m);

		public override int GetHashCode() =>
			HashCode.Combine(From, To, Kind, PromoteTo, PieceType, MovingSide, LeavesKingInCheck, CastleSide);

		public override string ToString() =>
			CastleSide switch
			{
				CastleSide.King  => "O-O",
				CastleSide.Queen => "O-O-O",
				_ => $"{(PieceType == ChessPieceType.Pawn ? "" : PieceType.ToChar(MovingSide).ToString())}{From}→{To}" +
					 (IsPromotion ? $" (promote to {PromoteTo.ToString()})" : string.Empty)                            +
					 (LeavesKingInCheck ? " (CHECK!)" : string.Empty)
			};

		public void Deconstruct(
			out BoardPosition from,
			out BoardPosition to,
			out PlayerColor movingSide,
			out ChessPieceType pieceType,
			out MoveKind kind,
			out PromotionPieceType promoteTo,
			out bool leavesKingInCheck)
		{
			from              = From;
			to                = To;
			movingSide        = MovingSide;
			pieceType         = PieceType;
			kind              = Kind;
			promoteTo         = PromoteTo;
			leavesKingInCheck = LeavesKingInCheck;
		}
	}
}
