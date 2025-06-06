using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;
using Bezoro.Core.Collections;

namespace Bezoro.Chess.Rules
{
	public sealed class StandardChessRules : IGameRules
	{
	#region Interface Implementations

		public IEnumerable<Move> FilterLegalMoves(GameModel game, IChessPieceModel piece, IEnumerable<Move> pseudoMoves)
		{
			var moves = pseudoMoves.ToList();
			if (moves.IsNullOrEmpty())
				return Enumerable.Empty<Move>();

			var evaluatedMoves = new List<Move>();
			foreach (var pseudoMove in moves)
			{
				var moveForValidation = new Move(
					pseudoMove.From,
					pseudoMove.To,
					piece.Color,
					piece.GetPieceType(),
					pseudoMove.Kind,
					pseudoMove.PromoteTo
				);

				if (!IsMoveBasicallyValid(game, moveForValidation, piece))
					continue;

				var leavesKingInCheck = CheckIfMoveExposesKing(game, moveForValidation, piece.Color);

				evaluatedMoves.Add(
					new(
						moveForValidation.From,
						moveForValidation.To,
						moveForValidation.MovingSide,
						moveForValidation.PieceType,
						moveForValidation.Kind,
						moveForValidation.PromoteTo,
						leavesKingInCheck
					));
			}

			return evaluatedMoves;
		}

	#endregion

		private bool CanCastle(GameModel game, Move castleMove, PlayerColor kingColor, IChessPieceModel kingPiece)
		{
			if (kingPiece == null || kingPiece.GetPieceType() != ChessPieceType.King || kingPiece.Color != kingColor)
			{
				return false;
			}

			var currentSnapshot = new BoardSnapshot(game.Board.Squares);
			var opponentColor   = kingColor == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;

			if (kingPiece.HasMoved)
			{
				return false;
			}

			var rookFile = castleMove.Kind == MoveKind.CastleKingside ? currentSnapshot.Width - 1 : 0;
			var kingRank = castleMove.From.Rank;

			var rookForCastle = currentSnapshot[rookFile, kingRank];
			if (rookForCastle?.GetPieceType() != ChessPieceType.Rook || rookForCastle.Color != kingColor ||
				rookForCastle.HasMoved)
			{
				return false;
			}

			if (currentSnapshot.IsSquareAttacked(castleMove.From, opponentColor))
			{
				return false;
			}

			var kingStartFile = castleMove.From.Column;

			if (castleMove.Kind == MoveKind.CastleKingside)
			{
				for (var file = kingStartFile + 1 ; file < rookFile ; ++file)
				{
					if (currentSnapshot[file, kingRank] != null) return false;
				}

				if (currentSnapshot.IsSquareAttacked(new(kingStartFile + 1, kingRank), opponentColor))
				{
					return false;
				}
			}
			else // CastleQueenside
			{
				for (var file = kingStartFile - 1 ; file > rookFile ; --file)
				{
					if (currentSnapshot[file, kingRank] != null) return false;
				}

				if (currentSnapshot.IsSquareAttacked(new(kingStartFile - 1, kingRank), opponentColor))
				{
					return false;
				}
			}

			return true;
		}

		private bool CheckIfMoveExposesKing(GameModel game, Move move, PlayerColor movingPlayerColor)
		{
			var           currentSnapshot = new BoardSnapshot(game.Board.Squares);
			BoardSnapshot nextBoardState;
			try
			{
				nextBoardState = currentSnapshot.ApplyMove(move);
			}
			catch (Exception)
			{
				return true;
			}

			var kingPosition = nextBoardState.FindKing(movingPlayerColor);

			if (kingPosition == null)
			{
				return true;
			}

			var opponentColor = movingPlayerColor == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
			return nextBoardState.IsSquareAttacked(kingPosition.Value, opponentColor);
		}

		private static bool IsFriendlyCapture(IChessBoardModel board, Move move)
		{
			if (move.Kind == MoveKind.CastleKingside || move.Kind == MoveKind.CastleQueenside)
			{
				return false;
			}

			var targetSquare = board.Squares[move.To.Column, move.To.Row];
			var targetPiece  = targetSquare.GetPiece();
			return targetPiece != null && targetPiece.Color == move.MovingSide;
		}

		// New helper method to check path for sliding pieces
		private bool IsSlidingPathClear(IChessBoardModel board, BoardPosition from, BoardPosition to)
		{
			var dCol = Math.Sign(to.Column - from.Column);
			var dRow = Math.Sign(to.Row    - from.Row);

			var currentCol = from.Column + dCol;
			var currentRow = from.Row + dRow;

			// Iterate through squares between 'from' and 'to'
			while (currentCol != to.Column || currentRow != to.Row)
			{
				if (board.Squares[currentCol, currentRow].GetPiece() != null)
				{
					return false; // Path is blocked
				}
				currentCol += dCol;
				currentRow += dRow;
			}
			return true; // Path is clear
		}

		private bool IsMoveBasicallyValid(GameModel game, Move move, IChessPieceModel movingPiece)
		{
			if (movingPiece       == null || movingPiece.GetPieceType() != move.PieceType ||
				movingPiece.Color != move.MovingSide)
			{
				return false;
			}

			if (IsFriendlyCapture(game.Board, move))
			{
				return false;
			}

			// --- NEW LOGIC FOR PATH OBSTRUCTION ---
			var pieceType = move.PieceType;

			// Knights jump over pieces, so they are not checked for path obstruction.
			if (pieceType == ChessPieceType.Rook || pieceType == ChessPieceType.Bishop || pieceType == ChessPieceType.Queen)
			{
				if (!IsSlidingPathClear(game.Board, move.From, move.To))
				{
					return false; // Path is blocked for this sliding piece
				}
			}
			else if (pieceType == ChessPieceType.Pawn)
			{
				// Check for obstruction on a two-square forward push.
				// This occurs if a pawn moves two rows forward and stays in the same column.
				if (move.From.Column == move.To.Column && Math.Abs(move.To.Row - move.From.Row) == 2)
				{
					// Determine the intermediate square's rank (the square being "jumped" over)
					var intermediateRank = move.From.Row + Math.Sign(move.To.Row - move.From.Row);
					if (game.Board.Squares[move.From.Column, intermediateRank].GetPiece() != null)
					{
						return false; // Intermediate square is blocked for the two-square pawn push
					}
				}
				// Note: Standard pawn captures (diagonal) and single-square pushes don't have intermediate path squares.
				// En Passant is a special case that would require its own logic if not handled by pseudo-move generation
				// or a specific MoveKind.
			}
			// --- END OF NEW LOGIC ---

			if (move.Kind == MoveKind.CastleKingside || move.Kind == MoveKind.CastleQueenside)
			{
				if (move.PieceType != ChessPieceType.King) return false;
				if (!CanCastle(game, move, move.MovingSide, movingPiece))
				{
					return false;
				}
			}
			else if (move.Kind == MoveKind.Promotion)
			{
				if (!move.PromoteTo.HasValue)
				{
					return false;
				}
			}

			return true;
		}
	}
}