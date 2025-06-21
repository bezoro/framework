using System.Collections.Generic;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves.Generation
{
	internal static class PawnMoveGenerator
	{
		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState)
		{
			Piece pawn         = gameState.Board.GetPiece(from);
			int   direction    = pawn.Color == PieceColor.White ? 1 : -1;
			int   startRow     = pawn.Color == PieceColor.White ? 1 : 6;
			int   promotionRow = pawn.Color == PieceColor.White ? 7 : 0;

			// Use helper methods to generate moves for each category
			foreach (Move move in GenerateAdvanceMoves(from, gameState, pawn, direction, startRow, promotionRow))
			{
				yield return move;
			}

			foreach (Move move in GenerateCaptureMoves(from, gameState, pawn, direction, promotionRow))
			{
				yield return move;
			}
		}

		/// <summary>
		///     Generates single and double square advances for a pawn.
		/// </summary>
		private static IEnumerable<Move> GenerateAdvanceMoves(
			Position from, GameState gameState, Piece pawn, int direction, int startRow, int promotionRow)
		{
			// 1. Single-square advance
			var oneStepForward = new Position(from.Row + direction, from.Col);
			if (BoardHelper.IsInsideBoard(oneStepForward) &&
				gameState.GetPieceAt(oneStepForward).Type == PieceType.None)
			{
				if (oneStepForward.Row == promotionRow)
				{
					foreach (Move move in GeneratePromotionMoves(from, oneStepForward, pawn, default))
					{
						yield return move;
					}
				}
				else
				{
					yield return Move.CreateNormal(from, oneStepForward, pawn);
				}

				// 2. Two-square advance (only possible if one-step advance is also possible)
				if (from.Row == startRow)
				{
					var twoStepsForward = new Position(from.Row + 2 * direction, from.Col);
					if (BoardHelper.IsInsideBoard(twoStepsForward) &&
						gameState.GetPieceAt(twoStepsForward).Type == PieceType.None)
					{
						yield return Move.CreateNormal(from, twoStepsForward, pawn);
					}
				}
			}
		}

		/// <summary>
		///     Generates standard diagonal captures and en passant captures.
		/// </summary>
		private static IEnumerable<Move> GenerateCaptureMoves(
			Position from, GameState gameState, Piece pawn, int direction, int promotionRow)
		{
			(int dRow, int dCol)[] captureVectors = { (direction, -1), (direction, 1) };
			foreach ((int dRow, int dCol) in captureVectors)
			{
				var toPosition = new Position(from.Row + dRow, from.Col + dCol);

				if (!BoardHelper.IsInsideBoard(toPosition))
				{
					continue;
				}

				// 3. Standard diagonal capture
				Piece pieceAtDestination = gameState.GetPieceAt(toPosition);
				if (pieceAtDestination.Type != PieceType.None && pieceAtDestination.Color != pawn.Color)
				{
					if (toPosition.Row == promotionRow)
					{
						foreach (Move move in GeneratePromotionMoves(from, toPosition, pawn, pieceAtDestination))
						{
							yield return move;
						}
					}
					else
					{
						yield return Move.CreateCapture(from, toPosition, pawn, pieceAtDestination);
					}
				}

				// 4. En passant capture
				if (gameState.EnPassantTargetSquare.IsValid && toPosition == gameState.EnPassantTargetSquare)
				{
					var   capturedPawnPosition = new Position(from.Row, toPosition.Col);
					Piece capturedPawn         = gameState.GetPieceAt(capturedPawnPosition);
					yield return Move.CreateEnPassant(from, toPosition, pawn, capturedPawn);
				}
			}
		}

		/// <summary>
		///     Generates all possible promotion moves (quiet or capture) for a pawn reaching the back rank.
		/// </summary>
		private static IEnumerable<Move> GeneratePromotionMoves(
			Position from, Position to, Piece pawn, Piece capturedPiece)
		{
			var promotionPieceTypes = new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

			foreach (PromotionType pieceType in promotionPieceTypes)
			{
				if (capturedPiece.Type == PieceType.None)
				{
					yield return Move.CreateQuietPromotion(from, to, pawn, pieceType);
				}
				else
				{
					yield return Move.CreateCapturePromotion(from, to, pawn, capturedPiece, pieceType);
				}
			}
		}
	}
}
