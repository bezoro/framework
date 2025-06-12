using System.Collections.Generic;

namespace Bezoro.Chess.ChessLogic.Generators
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
					yield return new(from, oneStepForward, pawn.Color, MoveType.PawnPromotion);
				}
				else
				{
					yield return new(from, oneStepForward, pawn.Color);
				}

				// 2. Two-square advance from starting position
				if (from.Row == startRow)
				{
					var twoStepsForward = new Position(from.Row + 2 * direction, from.Col);
					if (BoardHelper.IsInsideBoard(twoStepsForward) &&
						gameState.PiecePositions[twoStepsForward.Row, twoStepsForward.Col].Type == PieceType.None)
					{
						yield return new(from, twoStepsForward, pawn.Color);
					}
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
						yield return new(from, toPosition, pawn.Color, MoveType.PawnPromotion);
					}
					else
					{
						yield return new(from, toPosition, pawn.Color, MoveType.Capture);
					}
				}

				// 4. En passant capture
				if (gameState.EnPassantTargetSquare.HasValue                    &&
					toPosition.Row == gameState.EnPassantTargetSquare.Value.Row &&
					toPosition.Col == gameState.EnPassantTargetSquare.Value.Col)
				{
					yield return new(from, toPosition, pawn.Color, MoveType.EnPassant);
				}
			}
		}
	}
}
