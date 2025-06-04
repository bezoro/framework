using System;

namespace Bezoro.Core.Chess
{
	/// <summary>
	///     Immutable, lightweight value object that represents one *possible* move
	///     produced by a move-generator.
	///     It is pure data – it does not modify the board and has no side-effects.
	/// </summary>
	public sealed record Move(
		BoardPosition From,
		BoardPosition To,
		MoveKind Kind = MoveKind.Normal,
		PromotionPieceType? PromoteTo = null)
	{
		/// <summary>
		///     Factory helper for building promotion moves without having to specify the
		///     <see cref="MoveKind" /> argument manually.
		/// </summary>
		/// <exception cref="ArgumentNullException">
		///     Thrown when <paramref name="promoteTo" /> is <c>null</c>.
		/// </exception>
		public static Move Promotion(
			BoardPosition from,
			BoardPosition to,
			PromotionPieceType promoteTo) =>
			new(from, to, MoveKind.Promotion, promoteTo);

		/// <summary>
		///     Convenience helper: returns <c>true</c> if <see cref="Kind" /> is
		///     <see cref="MoveKind.Promotion" />.
		/// </summary>
		public bool IsPromotion => Kind == MoveKind.Promotion;
		public PlayerColor MovingSide { get; }
	}
}
