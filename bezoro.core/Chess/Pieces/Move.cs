using System;

namespace Bezoro.Core.Chess.Pieces
{
	/// <summary>
	///     Immutable, lightweight value type that represents one <em>possible</em> move
	///     produced by the move-generator.
	///     It is pure data – it does not modify the board and has no side-effects.
	/// </summary>
	public readonly struct Move : IEquatable<Move>
	{
		public Move(
			BoardPosition from,
			BoardPosition to,
			PlayerColor movingSide,
			MoveKind kind = MoveKind.Normal,
			PromotionPieceType? promoteTo = null)
		{
			From       = from;
			To         = to;
			MovingSide = movingSide;
			Kind       = kind;
			PromoteTo  = promoteTo;

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
		}

	#region Factory helpers

		public static Move Promotion(
			BoardPosition from,
			BoardPosition to,
			PlayerColor movingSide,
			PromotionPieceType promoteTo) =>
			new(from, to, movingSide, MoveKind.Promotion, promoteTo);

	#endregion

		/// <summary>
		///     Square the piece moves from.
		/// </summary>
		public BoardPosition From { get; }

		/// <summary>
		///     Square the piece moves to.
		/// </summary>
		public BoardPosition To { get; }

		/// <summary>
		///     Convenience helper.
		/// </summary>
		public bool IsPromotion => Kind == MoveKind.Promotion;

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
		///     object-initialiser if they need to tag the move with the side that generated it.
		/// </remarks>
		public PlayerColor MovingSide { get; init; }

	#region Equality & hashing

		public bool Equals(Move other) =>
			From.Equals(other.From) && To.Equals(other.To) && Kind == other.Kind && PromoteTo == other.PromoteTo;

		public override bool Equals(object? obj) =>
			obj is Move m && Equals(m);

		public override int GetHashCode() =>
			HashCode.Combine(From, To, Kind, PromoteTo);

		public static bool operator ==(Move left, Move right) => left.Equals(right);
		public static bool operator !=(Move left, Move right) => !left.Equals(right);

	#endregion

	#region Deconstruct & debugging

		public void Deconstruct(
			out BoardPosition from,
			out BoardPosition to,
			out MoveKind kind,
			out PromotionPieceType? promoteTo)
		{
			from      = From;
			to        = To;
			kind      = Kind;
			promoteTo = PromoteTo;
		}

		public override string ToString() =>
			$"{From}→{To}" + (IsPromotion ? $" (promote to {PromoteTo})" : string.Empty);

	#endregion
	}
}
