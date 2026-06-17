using System;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a strict UCI coordinate move with source, target, and optional promotion piece.
/// </summary>
public readonly record struct UciCoordinateMove
{
	private readonly string? _notation;

	private UciCoordinateMove(ChessSquare from, ChessSquare to, char? promotionPiece, string notation)
	{
		From = from;
		To = to;
		PromotionPiece = promotionPiece;
		_notation = notation;
	}

	/// <summary>
	///     Gets the source square.
	/// </summary>
	public ChessSquare From { get; }

	/// <summary>
	///     Gets the target square.
	/// </summary>
	public ChessSquare To { get; }

	/// <summary>
	///     Gets the normalized lowercase promotion piece, when the move promotes.
	/// </summary>
	public char? PromotionPiece { get; }

	/// <summary>
	///     Gets the normalized lowercase UCI notation.
	/// </summary>
	public string Notation => _notation ?? string.Empty;

	/// <summary>
	///     Gets whether this move includes a promotion piece.
	/// </summary>
	public bool IsPromotion => PromotionPiece.HasValue;

	/// <summary>
	///     Creates a normalized UCI coordinate move from parsed squares.
	/// </summary>
	/// <param name="from">Source square.</param>
	/// <param name="to">Target square.</param>
	/// <param name="promotionPiece">Optional promotion piece: <c>q</c>, <c>r</c>, <c>b</c>, or <c>n</c>.</param>
	/// <returns>Normalized coordinate move.</returns>
	/// <exception cref="ArgumentException">Thrown when the promotion suffix is invalid for the supplied squares.</exception>
	public static UciCoordinateMove Create(ChessSquare from, ChessSquare to, char? promotionPiece = null)
	{
		if (from == to)
			throw new ArgumentException(
				"Source and target squares must be different.",
				nameof(to)
			);

		char? normalizedPromotionPiece = null;
		if (promotionPiece.HasValue)
		{
			char piece = char.ToLowerInvariant(promotionPiece.Value);
			if (!IsPromotionPiece(piece))
				throw new ArgumentException(
					"Promotion piece must be one of 'q', 'r', 'b', or 'n'.",
					nameof(promotionPiece)
				);

			if (!IsPromotionTravel(from, to))
				throw new ArgumentException(
					"Promotion moves must travel to the final rank from the previous rank.",
					nameof(promotionPiece)
				);

			normalizedPromotionPiece = piece;
		}

		string notation = normalizedPromotionPiece.HasValue
			? $"{from}{to}{normalizedPromotionPiece.Value}"
			: $"{from}{to}";

		return new(from, to, normalizedPromotionPiece, notation);
	}

	/// <summary>
	///     Attempts to parse and normalize UCI coordinate notation.
	/// </summary>
	/// <param name="move">Candidate move such as <c>e2e4</c> or <c>a7a8q</c>.</param>
	/// <param name="coordinateMove">Parsed move when successful.</param>
	/// <returns><see langword="true" /> when the notation has valid squares and a valid promotion suffix.</returns>
	public static bool TryParse(string? move, out UciCoordinateMove coordinateMove)
	{
		coordinateMove = default;
		if (string.IsNullOrWhiteSpace(move))
			return false;

		string normalized = move.Trim().ToLowerInvariant();
		if (normalized.Length is not (4 or 5))
			return false;

		if (!ChessSquare.TryParse(normalized[..2], out var from) ||
			!ChessSquare.TryParse(normalized.Substring(2, 2), out var to))
			return false;

		if (from == to)
			return false;

		char? promotionPiece = null;
		if (normalized.Length == 5)
		{
			char candidate = normalized[4];
			if (!IsPromotionPiece(candidate) || !IsPromotionTravel(from, to))
				return false;

			promotionPiece = candidate;
		}

		coordinateMove = new(from, to, promotionPiece, normalized);
		return true;
	}

	/// <summary>
	///     Attempts to normalize UCI coordinate notation.
	/// </summary>
	/// <param name="move">Candidate move such as <c>e2e4</c> or <c>a7a8q</c>.</param>
	/// <param name="normalizedMove">Normalized lowercase notation when successful.</param>
	/// <returns><see langword="true" /> when the notation is a valid coordinate move.</returns>
	public static bool TryNormalize(string? move, out string normalizedMove)
	{
		if (TryParse(move, out var coordinateMove))
		{
			normalizedMove = coordinateMove.Notation;
			return true;
		}

		normalizedMove = string.Empty;
		return false;
	}

	/// <summary>
	///     Returns the normalized lowercase UCI notation.
	/// </summary>
	public override string ToString() => Notation;

	private static bool IsPromotionPiece(char value)
	{
		return value switch
		{
			'q' or 'r' or 'b' or 'n' => true,
			_ => false
		};
	}

	private static bool IsPromotionTravel(ChessSquare from, ChessSquare to)
	{
		int fileDelta = Math.Abs(to.FileIndex - from.FileIndex);
		if (fileDelta > 1)
			return false;

		if (from.RankIndex == ChessSquare.BoardSize - 2 && to.RankIndex == ChessSquare.BoardSize - 1)
			return true;

		return from.RankIndex == 1 && to.RankIndex == 0;
	}
}
