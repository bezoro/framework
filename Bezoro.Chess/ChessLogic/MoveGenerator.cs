using System;
using System.Collections.Generic;

namespace Bezoro.Chess.ChessLogic
{
	/// <summary>
	///     A static utility class for generating all possible pseudo-legal moves for a given game state.
	///     It does not currently check for moves that would leave the king in check.
	/// </summary>
	public static class MoveGenerator
	{
		/// <summary>
		///     Generates all moves for the currently active player.
		/// </summary>
		public static IEnumerable<Move> GenerateMoves(GameState gameState)
		{
			for (var r = 0 ; r < 8 ; r++)
			{
				for (var c = 0 ; c < 8 ; c++)
				{
					var piece = gameState.PiecePositions[r, c];

					// Skip empty squares or pieces of the inactive color
					if (piece.Type == default || piece.Color != gameState.ActiveColor)
					{
						continue;
					}

					var from = new Position(r, c);
					foreach (var move in GeneratePieceMoves(from, piece, gameState))
					{
						yield return move;
					}
				}
			}
		}

		/// <summary>
		///     Generates all valid moves for a specific piece at the given position.
		///     This can be used by the UI to show possible moves when a piece is selected.
		/// </summary>
		public static IEnumerable<Move> GeneratePieceMoves(Position from, GameState gameState)
		{
			var piece = gameState.PiecePositions[from.Row, from.Col];

			// Can't move an empty square or a piece of the wrong color
			if (piece.Type == default || piece.Color != gameState.ActiveColor)
			{
				yield break;
			}

			foreach (var move in GeneratePieceMoves(from, piece, gameState))
			{
				yield return move;
			}
		}

		private static bool IsInsideBoard(Position pos) =>
			pos.Row is >= 0 and < 8 && pos.Col is >= 0 and < 8;

		private static IEnumerable<Move> GenerateBishopMoves(Position from, GameState gameState)
		{
			// Define the four diagonal directions a bishop can move
			(int dRow, int dCol)[] directions = { (-1, -1), (-1, 1), (1, -1), (1, 1) };

			// For each direction, keep moving until we hit a piece or the edge of the board
			foreach (var (dRow, dCol) in directions)
			{
				// Start at position one step in the current direction
				var newRow = from.Row + dRow;
				var newCol = from.Col + dCol;

				// Keep going in this direction until we hit a piece or the edge
				while (IsInsideBoard(new(newRow, newCol)))
				{
					Position to                 = new(newRow, newCol);
					var      pieceAtDestination = gameState.PiecePositions[to.Row, to.Col];

					if (pieceAtDestination.Type == default)
					{
						// Empty square, can move here
						yield return new(from, to);
					}
					else if (pieceAtDestination.Color != gameState.ActiveColor)
					{
						// Enemy piece, can capture and then stop
						yield return new(from, to, MoveType.Capture);
						break;
					}
					else
					{
						// Friendly piece, can't move here or beyond
						break;
					}

					// Continue in this direction
					newRow += dRow;
					newCol += dCol;
				}
			}
		}

		private static IEnumerable<Move> GenerateKingMoves(Position from, GameState gameState)
		{
			// The king can move one square in any direction (8 possible moves)
			(int dRow, int dCol)[] directions =
			{
				(-1, -1), (-1, 0), (-1, 1),
				(0, -1), (0, 1),
				(1, -1), (1, 0), (1, 1)
			};

			// Normal king moves (one square in any direction)
			foreach (var (dRow, dCol) in directions)
			{
				Position to = new(from.Row + dRow, from.Col + dCol);

				if (!IsInsideBoard(to))
				{
					continue;
				}

				var pieceAtDestination = gameState.PiecePositions[to.Row, to.Col];

				if (pieceAtDestination.Type == default)
				{
					// Empty square, can move here
					yield return new(from, to);
				}
				else if (pieceAtDestination.Color != gameState.ActiveColor)
				{
					// Enemy piece, can capture
					yield return new(from, to, MoveType.Capture);
				}
			}

			// Castling moves
			// Note: We don't check if the king is in check or if squares are attacked,
			// this will be handled by a separate check validation step

			// White king castling
			if (gameState.ActiveColor == PieceColor.White && from.Equals(new(7, 4)))
			{
				// Kingside castling
				if ((gameState.Castling & CastlingRights.WhiteKingside) != 0)
				{
					var pathClear = gameState.PiecePositions[7, 5].Type == default &&
									gameState.PiecePositions[7, 6].Type == default;

					if (pathClear)
					{
						yield return new(from, new(7, 6), MoveType.CastleKingside);
					}
				}

				// Queenside castling
				if ((gameState.Castling & CastlingRights.WhiteQueenside) != 0)
				{
					var pathClear = gameState.PiecePositions[7, 3].Type == default &&
									gameState.PiecePositions[7, 2].Type == default &&
									gameState.PiecePositions[7, 1].Type == default;

					if (pathClear)
					{
						yield return new(from, new(7, 2), MoveType.CastleQueenside);
					}
				}
			}

			// Black king castling
			if (gameState.ActiveColor == PieceColor.Black && from.Equals(new(0, 4)))
			{
				// Kingside castling
				if ((gameState.Castling & CastlingRights.BlackKingside) != 0)
				{
					var pathClear = gameState.PiecePositions[0, 5].Type == default &&
									gameState.PiecePositions[0, 6].Type == default;

					if (pathClear)
					{
						yield return new(from, new(0, 6), MoveType.CastleKingside);
					}
				}

				// Queenside castling
				if ((gameState.Castling & CastlingRights.BlackQueenside) != 0)
				{
					var pathClear = gameState.PiecePositions[0, 3].Type == default &&
									gameState.PiecePositions[0, 2].Type == default &&
									gameState.PiecePositions[0, 1].Type == default;

					if (pathClear)
					{
						yield return new(from, new(0, 2), MoveType.CastleQueenside);
					}
				}
			}
		}

		private static IEnumerable<Move> GenerateKnightMoves(Position from, GameState gameState)
		{
			// Knights move in an "L" shape: two squares in one direction, then one in a perpendicular direction.
			(int dRow, int dCol)[] moves =
			{
				(2, 1), (2, -1), (-2, 1), (-2, -1),
				(1, 2), (1, -2), (-1, 2), (-1, -2)
			};

			foreach (var (dRow, dCol) in moves)
			{
				var toPosition = new Position(from.Row + dRow, from.Col + dCol);

				if (!IsInsideBoard(toPosition))
				{
					continue; // Skip moves that go off the board
				}

				var pieceAtDestination = gameState.PiecePositions[toPosition.Row, toPosition.Col];

				if (pieceAtDestination.Type == default)
				{
					// Empty square, can move here
					yield return new(from, toPosition);
				}
				else if (pieceAtDestination.Color != gameState.ActiveColor)
				{
					// Enemy piece, can capture
					yield return new(from, toPosition, MoveType.Capture);
				}
				// If it's a friendly piece, we can't move there.
			}
		}

		private static IEnumerable<Move> GeneratePawnMoves(Position from, GameState gameState)
		{
			var pawn         = gameState.PiecePositions[from.Row, from.Col];
			var direction    = pawn.Color == PieceColor.White ? -1 : 1;
			var startRow     = pawn.Color == PieceColor.White ? 6 : 1;
			var promotionRow = pawn.Color == PieceColor.White ? 0 : 7;

			// 1. Single-square advance
			var oneStepForward = new Position(from.Row + direction, from.Col);
			if (IsInsideBoard(oneStepForward) &&
				gameState.PiecePositions[oneStepForward.Row, oneStepForward.Col].Type == PieceType.None)
			{
				if (oneStepForward.Row == promotionRow)
				{
					yield return new(from, oneStepForward, MoveType.PawnPromotion);
				}
				else
				{
					yield return new(from, oneStepForward);
				}

				// 2. Two-square advance from starting position
				if (from.Row == startRow)
				{
					var twoStepsForward = new Position(from.Row + 2 * direction, from.Col);
					if (IsInsideBoard(twoStepsForward) &&
						gameState.PiecePositions[twoStepsForward.Row, twoStepsForward.Col].Type == PieceType.None)
					{
						yield return new(from, twoStepsForward);
					}
				}
			}

			// 3. Diagonal captures
			(int dRow, int dCol)[] captureMoves = { (direction, -1), (direction, 1) };
			foreach (var (dRow, dCol) in captureMoves)
			{
				var toPosition = new Position(from.Row + dRow, from.Col + dCol);

				if (!IsInsideBoard(toPosition))
				{
					continue;
				}

				// Standard capture
				var pieceAtDestination = gameState.PiecePositions[toPosition.Row, toPosition.Col];
				if (pieceAtDestination.Type != PieceType.None && pieceAtDestination.Color != pawn.Color)
				{
					if (toPosition.Row == promotionRow)
					{
						yield return new(from, toPosition, MoveType.PawnPromotion);
					}
					else
					{
						yield return new(from, toPosition, MoveType.Capture);
					}
				}

				// 4. En passant capture
				if (gameState.EnPassantTargetSquare.HasValue                    &&
					toPosition.Row == gameState.EnPassantTargetSquare.Value.Row &&
					toPosition.Col == gameState.EnPassantTargetSquare.Value.Col)
				{
					yield return new(from, toPosition, MoveType.EnPassant);
				}
			}
		}

		private static IEnumerable<Move> GeneratePieceMoves(Position from, Piece piece, GameState gameState) =>
			piece.Type switch
			{
				PieceType.Pawn   => GeneratePawnMoves(from, gameState),
				PieceType.Knight => GenerateKnightMoves(from, gameState),
				PieceType.Bishop => GenerateBishopMoves(from, gameState),
				PieceType.Rook   => GenerateRookMoves(from, gameState),
				PieceType.Queen  => GenerateQueenMoves(from, gameState),
				PieceType.King   => GenerateKingMoves(from, gameState),
				_                => Array.Empty<Move>() // Should not be reachable
			};

		// Queen moves are a combination of Rook and Bishop moves
		private static IEnumerable<Move> GenerateQueenMoves(Position from, GameState gameState)
		{
			foreach (var move in GenerateRookMoves(from, gameState))
			{
				yield return move;
			}

			foreach (var move in GenerateBishopMoves(from, gameState))
			{
				yield return move;
			}
		}

		private static IEnumerable<Move> GenerateRookMoves(Position from, GameState gameState)
		{
			// Rooks move any number of squares along a rank or file
			(int dRow, int dCol)[] directions = { (0, 1), (0, -1), (1, 0), (-1, 0) };

			foreach (var (dRow, dCol) in directions)
			{
				for (var i = 1 ; i < 8 ; i++)
				{
					var toRow = from.Row + i * dRow;
					var toCol = from.Col + i * dCol;

					if (!IsInsideBoard(new(toRow, toCol)))
					{
						break; // Off the board
					}

					var toPosition         = new Position(toRow, toCol);
					var pieceAtDestination = gameState.PiecePositions[toRow, toCol];

					if (pieceAtDestination.Type == default)
					{
						// Empty square, can move here
						yield return new(from, toPosition);
					}
					else
					{
						if (pieceAtDestination.Color != gameState.ActiveColor)
						{
							// Enemy piece, can capture
							yield return new(from, toPosition, MoveType.Capture);
						}

						// Path is blocked by a piece (friendly or enemy), so stop in this direction
						break;
					}
				}
			}
		}
	}
}
