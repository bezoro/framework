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
					foreach (var move in GeneratePromotionMoves(from, oneStepForward, pawn.Color, PieceType.None))
					{
						yield return move;
					}
				}
				else
				{
					yield return new(from, oneStepForward, pawn.Color);
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
					yield return new(from, twoStepsForward, pawn.Color);
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
									 from, toPosition, pawn.Color, pieceAtDestination.Type))
						{
							yield return move;
						}
					}
					else
					{
						// Standard capture, now storing the captured piece type.
						yield return new(
							from, toPosition, pawn.Color, MoveType.Capture, pieceAtDestination.Type,
							PromotionType.None);
					}
				}

				// 4. En passant capture
				if (gameState.EnPassantTargetSquare.HasValue                    &&
					toPosition.Row == gameState.EnPassantTargetSquare.Value.Row &&
					toPosition.Col == gameState.EnPassantTargetSquare.Value.Col)
				{
					// En-passant capture, the captured piece is always a Pawn.
					yield return new(
						from, toPosition, pawn.Color, MoveType.EnPassant, PieceType.Pawn, PromotionType.None);
				}
			}
		}

		/// <summary>
		///     Generates all possible promotion moves (quiet or capture) for a pawn reaching the back rank.
		/// </summary>
		private static IEnumerable<Move> GeneratePromotionMoves(
			Position from, Position to, PieceColor color, PieceType capturedPiece)
		{
			var moveType = capturedPiece == PieceType.None ? MoveType.PawnPromotion : MoveType.PawnPromotionCapture;

			yield return new(from, to, color, moveType, capturedPiece, PromotionType.Queen);
			yield return new(from, to, color, moveType, capturedPiece, PromotionType.Rook);
			yield return new(from, to, color, moveType, capturedPiece, PromotionType.Bishop);
			yield return new(from, to, color, moveType, capturedPiece, PromotionType.Knight);
		}
	}
}
