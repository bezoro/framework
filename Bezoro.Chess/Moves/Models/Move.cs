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
		public Move(
			BoardPosition from,
			BoardPosition to,
			PlayerColor movingSide,
			ChessPieceType pieceType,
			MoveKind kind = MoveKind.Normal,
			PromotionPieceType? promoteTo = null,
			bool leavesKingInCheck = false)
		{
			From              = from;
			To                = to;
			MovingSide        = movingSide;
			PieceType         = pieceType;
			Kind              = kind;
			PromoteTo         = promoteTo;
			LeavesKingInCheck = leavesKingInCheck;

			if (Kind == MoveKind.Promotion && promoteTo is null)
			{
				throw new ArgumentNullException(
					nameof(promoteTo),
					"A promotion move must specify the piece to promote to.");
			}

			if (Kind != MoveKind.Promotion && promoteTo is not null)
			{
				throw new ArgumentException(
					"PromoteTo may only be supplied for promotion moves.",
					nameof(promoteTo));
			}

			if ((Kind == MoveKind.CastleKingside || Kind == MoveKind.CastleQueenside) &&
				pieceType != ChessPieceType.King)
			{
				throw new ArgumentException(
					"Castling moves must have a PieceType of King.",
					nameof(pieceType));
			}
		}

		public static bool operator ==(Move left, Move right) => left.Equals(right);
		public static bool operator !=(Move left, Move right) => !left.Equals(right);

		public static Move CastleKingSide(
			BoardPosition kingFrom,
			BoardPosition kingTo,
			PlayerColor movingSide) =>
			new(kingFrom, kingTo, movingSide, ChessPieceType.King, MoveKind.CastleKingside);

		public static Move CastleQueenSide(
			BoardPosition kingFrom,
			BoardPosition kingTo,
			PlayerColor movingSide) =>
			new(kingFrom, kingTo, movingSide, ChessPieceType.King, MoveKind.CastleQueenside);

		public static Move Promotion(
			BoardPosition from,
			BoardPosition to,
			PlayerColor movingSide,
			PromotionPieceType promoteTo) =>
			new(from, to, movingSide, ChessPieceType.Pawn, MoveKind.Promotion, promoteTo);

		/// <summary>
		///     Square the piece moves from.
		/// </summary>
		public BoardPosition From { get; }

		/// <summary>
		///     Square the piece moves to.
		/// </summary>
		public BoardPosition To { get; }

		/// <summary>
		///     Convenience helper for king-side castling.
		/// </summary>
		public bool IsCastleKingSide => Kind == MoveKind.CastleKingside;

		/// <summary>
		///     Convenience helper for queen-side castling.
		/// </summary>
		public bool IsCastleQueenSide => Kind == MoveKind.CastleQueenside;

		/// <summary>
		///     Convenience helper.
		/// </summary>
		public bool IsPromotion => Kind == MoveKind.Promotion;

		public bool LeavesKingInCheck { get; }

		public ChessPieceType PieceType { get; }

		/// <summary>
		///     Flags describing the kind of move (normal, capture, castling, promotion, …).
		/// </summary>
		public MoveKind Kind { get; }

		/// <summary>
		///     Target piece type when <see cref="Kind" /> is <see cref="MoveKind.Promotion" />.
		///     <c>null</c> otherwise.
		/// </summary>
		public PromotionPieceType? PromoteTo { get; }

		/// <remarks>
		///     This is intentionally left as the default value; callers may set it through an
		///     object-initializer if they need to tag the move with the side that generated it.
		/// </remarks>
		public PlayerColor MovingSide { get; init; }

	#region Interface Implementations

		public bool Equals(Move other) =>
			From.Equals(other.From)              && To.Equals(other.To)            &&
			Kind              == other.Kind      && PromoteTo  == other.PromoteTo  &&
			PieceType         == other.PieceType && MovingSide == other.MovingSide &&
			LeavesKingInCheck == other.LeavesKingInCheck;

	#endregion

		public override bool Equals(object? obj) =>
			obj is Move m && Equals(m);

		public override int GetHashCode() =>
			HashCode.Combine(From, To, Kind, PromoteTo, PieceType, MovingSide, LeavesKingInCheck);

		public override string ToString() =>
			Kind switch
			{
				MoveKind.CastleKingside  => "O-O",
				MoveKind.CastleQueenside => "O-O-O",
				_ => $"{(PieceType == ChessPieceType.Pawn ? "" : PieceType.ToChar(MovingSide).ToString())}{From}→{To}" +
					 (IsPromotion ? $" (promote to {PromoteTo?.ToString()})" : string.Empty)                           +
					 (LeavesKingInCheck ? " (CHECK!)" : string.Empty)
			};

		public void Deconstruct(
			out BoardPosition from,
			out BoardPosition to,
			out PlayerColor movingSide,
			out ChessPieceType pieceType,
			out MoveKind kind,
			out PromotionPieceType? promoteTo,
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
