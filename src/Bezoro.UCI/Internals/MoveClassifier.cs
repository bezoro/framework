using System;
using Bezoro.UCI.Types;

namespace Bezoro.UCI.Internals
{
	/// <summary>
	///     Helper class for classifying chess moves by their type.
	/// </summary>
	internal static class MoveClassifier
	{
		/// <summary>
		///     A struct to hold the parsed components of a move.
		/// </summary>
		internal readonly struct ParsedMove
		{
			public ParsedMove(string from, string to, char promotionChar)
			{
				PromotionChar = promotionChar;
				From          = from;
				To            = to;
			}

			public char   PromotionChar { get; }
			public string From          { get; }
			public string To            { get; }
		}

		/// <summary>
		///     Analyzes a single move based on the current board state to determine its type.
		/// </summary>
		public static MoveClassification ClassifyMove(string move, BoardState boardState)
		{
			if (string.IsNullOrEmpty(move) || move.Length < 4)
			{
				throw new InvalidOperationException($"Invalid move '{move}'.");
			}

			var parsedMove = new ParsedMove(
				move[..2],
				move[2..4],
				move.Length == 5 ? move[4] : ' '
			);

			if (!boardState.PiecePositions.TryGetValue(parsedMove.From, out char movingPiece))
			{
				// This indicates a severe inconsistency between the board state and the move.
				throw new InvalidOperationException(
					$"No piece found at the 'From' square '{parsedMove.From}' for move '{move}'.");
			}

			bool isCaptureOnToSquare = boardState.PiecePositions.ContainsKey(parsedMove.To);

			if (IsCastling(parsedMove, movingPiece))
			{
				return new MoveClassification(move) { IsCastling = true };
			}

			if (IsEnPassant(parsedMove, movingPiece, isCaptureOnToSquare, boardState))
			{
				return new MoveClassification(move) { IsCapture = true, IsEnPassant = true };
			}

			if (IsPromotion(movingPiece, parsedMove.PromotionChar))
			{
				return new MoveClassification(move) { IsPromotion = true, IsCapture = isCaptureOnToSquare };
			}

			if (isCaptureOnToSquare)
			{
				return new MoveClassification(move) { IsCapture = true };
			}

			return new MoveClassification(move);
		}

		private static bool IsCastling(ParsedMove move, char movingPiece) =>
			char.ToLower(movingPiece) == 'k' && Math.Abs(move.From[0] - move.To[0]) == 2;

		private static bool IsEnPassant(ParsedMove move, char movingPiece, bool isCapture, BoardState boardState) =>
			char.ToLower(movingPiece) == 'p'        &&
			move.From[0]              != move.To[0] &&
			!isCapture                              &&
			move.To.Equals(boardState.EnPassantTarget, StringComparison.OrdinalIgnoreCase);

		private static bool IsPromotion(char movingPiece, char promotionChar) =>
			char.ToLower(movingPiece) == 'p' && promotionChar != ' ';
	}
}
