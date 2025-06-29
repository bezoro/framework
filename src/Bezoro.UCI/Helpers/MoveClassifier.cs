using System;
using Bezoro.Core;
using Bezoro.UCI.Types;

namespace Bezoro.UCI.Helpers
{
	/// <summary>
	///     Helper class for classifying chess moves by their type.
	/// </summary>
	internal static class MoveClassifier
	{
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

			if (IsCastling(parsedMove, movingPiece, boardState))
			{
				return new MoveClassification(move) { IsCastling = true };
			}

			if (IsEnPassant(parsedMove, movingPiece, isCaptureOnToSquare, boardState))
			{
				Logger.LogInfo(parsedMove.To);
				return new MoveClassification(move) { IsCapture = true, IsEnPassant = true };
			}

			if (IsPromotion(parsedMove, movingPiece))
			{
				return new MoveClassification(move) { IsPromotion = true, IsCapture = isCaptureOnToSquare };
			}

			if (isCaptureOnToSquare)
			{
				return new MoveClassification(move) { IsCapture = true };
			}

			return new MoveClassification(move);
		}

		private static bool IsCastling(ParsedMove move, char movingPiece, BoardState boardState)
		{
			if (char.ToLower(movingPiece) != 'k' || Math.Abs(move.From[0] - move.To[0]) != 2)
			{
				return false;
			}

			bool isKingside = move.To[0] == 'g';
			return boardState.ActiveColor switch
			{
				'w' => isKingside ? boardState.CastlingRights.Contains('K') : boardState.CastlingRights.Contains('Q'),
				'b' => isKingside ? boardState.CastlingRights.Contains('k') : boardState.CastlingRights.Contains('q'),
				_   => false
			};
		}

		private static bool IsEnPassant(ParsedMove move, char movingPiece, bool isCapture, BoardState boardState) =>
			char.ToLower(movingPiece) == 'p'        &&
			move.From[0]              != move.To[0] &&
			!isCapture                              &&
			move.To.Equals(boardState.EnPassantTarget, StringComparison.OrdinalIgnoreCase);

		private static bool IsPromotion(ParsedMove move, char movingPiece)
		{
			if (char.ToLower(movingPiece) != 'p' || move.PromotionChar == ' ')
			{
				return false;
			}

			char toRank      = move.To[1];
			bool isWhitePawn = char.IsUpper(movingPiece);
			return isWhitePawn && toRank == '8' || !isWhitePawn && toRank == '1';
		}

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
	}
}
