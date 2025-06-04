using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Core.Chess.Interfaces;
using Bezoro.Core.Chess.Pieces;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess.Rules
{
	public sealed class StandardChessRules : IGameRules
	{
	#region Interface Implementations

		public IEnumerable<Move> FilterLegalMoves(GameModel game, IChessPieceModel piece, IEnumerable<Move> pseudoMoves)
		{
			if (pseudoMoves == null)
			{
				return Enumerable.Empty<Move>();
			}

			var legalMoves = new List<Move>();
			foreach (var move in pseudoMoves)
			{
				if (IsMoveLegal(game, move))
				{
					legalMoves.Add(move);
				}
			}

			return legalMoves;
		}

	#endregion

		private bool CanCastle(GameModel game, Move castleMove, PlayerColor kingColor, IChessPieceModel kingPiece)
		{
			var currentSnapshot = new BoardSnapshot(game.Board.Squares);
			var opponentColor   = kingColor == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;

			// 0. King and relevant Rook must not have moved.
			if (kingPiece.HasMoved)
			{
				return false; // King has moved.
			}

			var rookFile = castleMove.Kind == MoveKind.CastleKingside ? currentSnapshot.Width - 1 : 0;
			var kingRank = castleMove.From.Rank; // King's current rank

			var rookForCastle = currentSnapshot[rookFile, kingRank];
			if (rookForCastle                   == null
				|| rookForCastle.GetPieceType() != ChessPieceType.Rook
				|| rookForCastle.Color          != kingColor)
			{
				return false; // Rook not found, wrong type, or wrong color.
			}

			if (rookForCastle.HasMoved)
			{
				return false; // Rook has moved.
			}

			// 1. King must not be in current check.
			var kingInitialPosition = castleMove.From; // King's current position from the move
			if (currentSnapshot.IsSquareAttacked(kingInitialPosition, opponentColor))
			{
				return false; // Cannot castle out of check.
			}

			// 2. Squares between King and Rook must be empty.
			// 3. King must not pass through an attacked square.
			int kingStartFile = castleMove.From.File;

			if (castleMove.Kind == MoveKind.CastleKingside)
			{
				// Squares between king (e.g., E file) and H-file rook: F, G files.
				for (var file = kingStartFile + 1 ; file < rookFile ; ++file)
				{
					if (currentSnapshot[file, kingRank] != null) return false; // Path not clear
				}

				// King passes over (kingStartFile + 1). King lands on (kingStartFile + 2).
				if (currentSnapshot.IsSquareAttacked(new(kingStartFile + 1, kingRank), opponentColor))
				{
					return false; // King passes through check.
				}
			}
			else // CastleQueenside
			{
				// Squares between king (e.g., E file) and A-file rook: D, C, B files.
				for (var file = kingStartFile - 1 ; file > rookFile ; --file)
				{
					if (currentSnapshot[file, kingRank] != null) return false; // Path not clear
				}

				// King passes over (kingStartFile - 1). King lands on (kingStartFile - 2).
				if (currentSnapshot.IsSquareAttacked(new(kingStartFile - 1, kingRank), opponentColor))
				{
					return false; // King passes through check.
				}
			}

			// Note: The destination square (where the king lands) will be checked by MoveLeavesKingInCheck.
			return true;
		}

		private static bool IsFriendlyCapture(
			IChessBoardModel board,
			Move move,
			IChessPieceModel movingPiece)
		{
			var targetSquare = board.Squares[move.To.File, move.To.Rank];
			var targetPiece  = targetSquare.GetPiece();

			return targetPiece != null && targetPiece.Color == movingPiece.Color;
		}

		private bool IsMoveLegal(GameModel game, Move move)
		{
			var board = game.Board;

			// 1) The piece to move must actually be on the source square
			if (!TryGetMovingPiece(board, move, out var movingPiece) || movingPiece == null)
				return false;

			// 2) The destination square must not contain a friendly piece (for regular moves)
			if (move.Kind    != MoveKind.CastleKingside
				&& move.Kind != MoveKind.CastleQueenside)
			{
				if (IsFriendlyCapture(board, move, movingPiece))
					return false;
			}

			// 3) Specific rules for move kinds (Castling, Promotion)
			if (move.Kind == MoveKind.CastleKingside || move.Kind == MoveKind.CastleQueenside)
			{
				if (movingPiece.GetPieceType() != ChessPieceType.King) return false; // Only kings can castle
				if (!CanCastle(game, move, movingPiece.Color, movingPiece))
				{
					return false;
				}
			}
			else if (move.Kind == MoveKind.Promotion)
			{
				if (!move.PromoteTo.HasValue)
				{
					return false; // Promotion move must specify promotion piece type.
				}
			}

			// 4) The move must not leave the moving player's king in check.
			if (MoveLeavesKingInCheck(game, move, movingPiece.Color))
				return false;

			return true;
		}

		private bool MoveLeavesKingInCheck(GameModel game, Move move, PlayerColor movingPlayerColor)
		{
			var           currentSnapshot = new BoardSnapshot(game.Board.Squares);
			BoardSnapshot nextBoardSnapshot;
			try
			{
				nextBoardSnapshot = currentSnapshot.ApplyMove(move);
			}
			catch (Exception)
			{
				// If applying the move itself is problematic (e.g. bad promotion data, invalid castle state per snapshot),
				// consider it as leading to an illegal state or leaving king in check.
				return true;
			}

			var kingPosition = nextBoardSnapshot.FindKing(movingPlayerColor);

			if (kingPosition == null)
			{
				// King disappeared, which is illegal.
				return true;
			}

			var opponentColor = movingPlayerColor == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
			return nextBoardSnapshot.IsSquareAttacked(kingPosition.Value, opponentColor);
		}

		private static bool TryGetMovingPiece(
			IChessBoardModel board,
			Move move,
			out IChessPieceModel? movingPiece)
		{
			var sourceSquare = board.Squares[move.From.File, move.From.Rank];
			movingPiece = sourceSquare.GetPiece();
			return movingPiece != null;
		}
	}
}
