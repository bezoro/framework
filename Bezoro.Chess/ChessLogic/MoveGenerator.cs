using System;
using System.Collections.Generic;

namespace ChessLogic
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
						yield return new(from, to);
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

				// Can move to an empty square or capture an enemy piece
				if (pieceAtDestination.Type == default || pieceAtDestination.Color != gameState.ActiveColor)
				{
					yield return new(from, to);
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
			// 8 potential L-shaped moves
			int[] dRow = { 2, 2, 1, 1, -1, -1, -2, -2 };
			int[] dCol = { 1, -1, 2, -2, 2, -2, 1, -1 };

			for (var i = 0 ; i < 8 ; i++)
			{
				Position to = new(from.Row + dRow[i], from.Col + dCol[i]);

				if (!IsInsideBoard(to))
				{
					continue;
				}

				var destinationPiece = gameState.PiecePositions[to.Row, to.Col];
				// Can move to an empty square or a square occupied by an opponent's piece
				if (destinationPiece.Type == default || destinationPiece.Color != gameState.ActiveColor)
				{
					yield return new(from, to);
				}
			}
		}

		private static IEnumerable<Move> GeneratePawnMoves(Position from, GameState gameState)
		{
			// Pawns move differently based on their color
			var direction = gameState.ActiveColor == PieceColor.White ? -1 : 1;
			var isAtStartingRank = gameState.ActiveColor == PieceColor.White && from.Row == 6 ||
								   gameState.ActiveColor == PieceColor.Black && from.Row == 1;

			var isAtPromotionRank = gameState.ActiveColor == PieceColor.White && from.Row == 1 ||
									gameState.ActiveColor == PieceColor.Black && from.Row == 6;

			// Forward move (one square)
			Position oneStepForward = new(from.Row + direction, from.Col);
			if (IsInsideBoard(oneStepForward) &&
				gameState.PiecePositions[oneStepForward.Row, oneStepForward.Col].Type == default)
			{
				// Pawn promotion
				if (isAtPromotionRank)
				{
					yield return new(from, oneStepForward, MoveType.PawnPromotion);
				}
				else
				{
					yield return new(from, oneStepForward);
				}

				// Initial two-square move
				if (isAtStartingRank)
				{
					Position twoStepsForward = new(from.Row + 2 * direction, from.Col);

					if (gameState.PiecePositions[twoStepsForward.Row, twoStepsForward.Col].Type == default)
					{
						yield return new(from, twoStepsForward);
					}
				}
			}

			// Captures (diagonal moves)
			for (var dc = -1 ; dc <= 1 ; dc += 2)
			{
				Position captureDiagonal = new(from.Row + direction, from.Col + dc);

				if (IsInsideBoard(captureDiagonal))
				{
					var pieceAtCapture = gameState.PiecePositions[captureDiagonal.Row, captureDiagonal.Col];

					// Normal capture
					if (pieceAtCapture.Type != default && pieceAtCapture.Color != gameState.ActiveColor)
					{
						if (isAtPromotionRank)
						{
							yield return new(from, captureDiagonal, MoveType.PawnPromotion);
						}
						else
						{
							yield return new(from, captureDiagonal);
						}
					}

					// En passant capture
					if (gameState.EnPassantTargetSquare.HasValue &&
						captureDiagonal.Equals(gameState.EnPassantTargetSquare.Value))
					{
						yield return new(from, captureDiagonal, MoveType.EnPassant);
					}
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
			// Define the four directions a rook can move: up, right, down, left
			(int dRow, int dCol)[] directions = { (-1, 0), (0, 1), (1, 0), (0, -1) };

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
						yield return new(from, to);
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
	}
}
