using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Helpers;
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
			if (pseudoMoves.IsNullOrEmpty())
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
			var kingStartFile = castleMove.From.Column;

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
			PlayerColor movingPieceColor)
		{
			var targetSquare = board.Squares[move.To.Column, move.To.Row];
			var targetPiece  = targetSquare.GetPiece();

			return targetPiece != null && targetPiece.Color == movingPieceColor;
		}

		private bool IsMoveLegal(GameModel game, Move move)
		{
			var board      = game.Board;
			var pieceType  = move.PieceType;
			var pieceColor = move.MovingSide;

			// 1) The piece to move must actually be on the source square
			var movingPiece = move.GetMovingPiece(board);
			if (movingPiece == null)
				return false; // Piece to move not found on source square.

			// 2) The destination square must not contain a friendly piece (for regular moves)
			if (move.Kind    != MoveKind.CastleKingside
				&& move.Kind != MoveKind.CastleQueenside)
			{
				if (IsFriendlyCapture(board, move, pieceColor))
					return false;
			}

			// 3) Specific rules for move kinds (Castling, Promotion)
			if (move.Kind == MoveKind.CastleKingside || move.Kind == MoveKind.CastleQueenside)
			{
				if (pieceType != ChessPieceType.King) return false; // Only kings can castle
				if (!CanCastle(game, move, pieceColor, movingPiece))
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
			if (MoveLeavesKingInCheck(game, move, pieceColor))
				return false;

			return true;
		}

		// TODO: Implement
		private bool MoveLeavesKingInCheck(GameModel game, Move move, PlayerColor movingPlayerColor) =>
			throw new NotImplementedException();
	}
}
