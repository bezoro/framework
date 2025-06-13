using System.Collections.Generic;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Domain.Moves.Generation
{
	internal static class PawnMoveGenerator
	{
		public static IEnumerable<Move> GenerateMoves(Position from, GameState gameState)
		{
			var pawn         = gameState.PiecePositions[from.Row, from.Col];
			var direction    = pawn.Color == PieceColor.White ? -1 : 1;
			var startRow     = pawn.Color == PieceColor.White ? 6 : 1;
			var promotionRow = pawn.Color == PieceColor.White ? 0 : 7;

			// 1. Single-square advance
			var oneStepForward = new Position(from.Row + direction, from.Col);
			if (BoardHelper.IsInsideBoard(oneStepForward) &&
				gameState.PiecePositions[oneStepForward.Row, oneStepForward.Col].Type == PieceType.None)
			{
				if (oneStepForward.Row == promotionRow)
				{
					// Quiet promotion: generate a move for each possible promotion piece.
					foreach (var move in GeneratePromotionMoves(from, oneStepForward, pawn, default))
					{
						yield return move;
					}
				}
				else
				{
					yield return Move.CreateNormal(from, oneStepForward, pawn);
				}
			}

			// 2. Two-square advance from starting position
			if (from.Row == startRow)
			{
				var oneStepIsEmpty  = gameState.PiecePositions[from.Row + direction, from.Col].Type == PieceType.None;
				var twoStepsForward = new Position(from.Row + 2 * direction, from.Col);
				if (oneStepIsEmpty                             &&
					BoardHelper.IsInsideBoard(twoStepsForward) &&
					gameState.PiecePositions[twoStepsForward.Row, twoStepsForward.Col].Type == PieceType.None)
				{
					yield return Move.CreateNormal(from, twoStepsForward, pawn);
				}
			}

			// 3. Diagonal captures
			(int dRow, int dCol)[] captureMoves = { (direction, -1), (direction, 1) };
			foreach (var (dRow, dCol) in captureMoves)
			{
				var toPosition = new Position(from.Row + dRow, from.Col + dCol);

				if (!BoardHelper.IsInsideBoard(toPosition))
				{
					continue;
				}

				// Standard capture
				var pieceAtDestination = gameState.PiecePositions[toPosition.Row, toPosition.Col];
				if (pieceAtDestination.Type != PieceType.None && pieceAtDestination.Color != pawn.Color)
				{
					if (toPosition.Row == promotionRow)
					{
						// Capture-promotion: generate a move for each possible promotion piece.
						foreach (var move in GeneratePromotionMoves(
									 from, toPosition, pawn, pieceAtDestination))
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
				if (gameState.EnPassantTargetSquare.HasValue                    &&
					toPosition.Row == gameState.EnPassantTargetSquare.Value.Row &&
					toPosition.Col == gameState.EnPassantTargetSquare.Value.Col)
				{
					var capturedPawnPosition = new Position(from.Row, toPosition.Col);
					var capturedPawn = gameState.PiecePositions[capturedPawnPosition.Row, capturedPawnPosition.Col];
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

			foreach (var pieceType in promotionPieceTypes)
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
