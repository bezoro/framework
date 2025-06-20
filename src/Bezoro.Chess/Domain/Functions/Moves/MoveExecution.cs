using System;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves.Execution;

namespace Bezoro.Chess.Domain.Moves
{
	/// <summary>
	///     Responsible for applying moves to a game state and producing a new, updated game state.
	/// </summary>
	internal static class MoveExecution
	{
		/// <summary>
		///     Executes <paramref name="move" /> on <paramref name="state" /> and returns the resulting state.
		/// </summary>
		public static GameState ExecuteMove(GameState state, Move move)
		{
			// Get the piece that's moving
			Piece movingPiece = state.PiecePositions[move.From.Row, move.From.Col];

			// Determine the en passant target square for the *next* state.
			// This is only set when a pawn makes a two-square advance.
			Position? newEnPassantTargetSquare = null;
			if (movingPiece.Type == PieceType.Pawn && Math.Abs(move.From.Row - move.To.Row) == 2)
			{
				// The target square is the one "behind" the pawn's destination.
				int behindRow = move.From.Row + (move.To.Row - move.From.Row) / 2;
				newEnPassantTargetSquare = new Position(behindRow, move.From.Col);
			}

			// Create a new game state (we're using an immutable approach)
			var newState = new GameState
			{
				// Copy the current board
				PiecePositions = (Piece[,])state.PiecePositions.Clone(),

				// Switch the active color
				ActiveColor = state.ActiveColor == PieceColor.White ? PieceColor.Black : PieceColor.White,

				// Update the move counters
				HalfMoveClock = ShouldResetHalfMoveClock(state, move) ? 0 : state.HalfMoveClock + 1,
				FullMoveNumber = state.ActiveColor == PieceColor.Black
					? state.FullMoveNumber + 1
					: state.FullMoveNumber,

				// These will be updated as needed below
				Castling              = UpdateCastlingRights(state, move),
				EnPassantTargetSquare = newEnPassantTargetSquare
			};

			// Delegate the actual board modification to the correct executor.
			ExecuteMoveOnBoard(newState, move);

			return newState;
		}

		private static bool ShouldResetHalfMoveClock(GameState state, Move move)
		{
			// Half-move clock resets on pawn moves or captures
			if (state.PiecePositions[move.From.Row, move.From.Col].Type == PieceType.Pawn)
			{
				return true;
			}

			if (state.PiecePositions[move.To.Row, move.To.Col].Type != default)
			{
				return true;
			}

			if (move.Type == MoveType.EnPassant)
			{
				return true;
			}

			return false;
		}

		private static CastlingRights UpdateCastlingRights(GameState state, Move move)
		{
			CastlingRights newRights   = state.Castling;
			Piece          movingPiece = state.PiecePositions[move.From.Row, move.From.Col];

			// King moves remove all castling rights for that color
			if (movingPiece.Type == PieceType.King)
			{
				if (state.ActiveColor == PieceColor.White)
				{
					newRights &= ~CastlingRights.White;
				}
				else
				{
					newRights &= ~CastlingRights.Black;
				}
			}

			// A rook moving from its home square removes the castling right for that side
			if (movingPiece.Type == PieceType.Rook)
			{
				if (move.From.Row == 7) // White's back rank
				{
					if (move.From.Col == 0)
					{
						newRights &= ~CastlingRights.WhiteQueenside;
					}

					if (move.From.Col == 7)
					{
						newRights &= ~CastlingRights.WhiteKingside;
					}
				}
				else if (move.From.Row == 0) // Black's back rank
				{
					if (move.From.Col == 0)
					{
						newRights &= ~CastlingRights.BlackQueenside;
					}

					if (move.From.Col == 7)
					{
						newRights &= ~CastlingRights.BlackKingside;
					}
				}
			}

			// If a rook is captured on its home square, remove the corresponding castling right
			if (move.To.Row == 0)
			{
				if (move.To.Col == 0)
				{
					newRights &= ~CastlingRights.BlackQueenside;
				}

				if (move.To.Col == 7)
				{
					newRights &= ~CastlingRights.BlackKingside;
				}
			}
			else if (move.To.Row == 7)
			{
				if (move.To.Col == 0)
				{
					newRights &= ~CastlingRights.WhiteQueenside;
				}

				if (move.To.Col == 7)
				{
					newRights &= ~CastlingRights.WhiteKingside;
				}
			}

			return newRights;
		}

		/// <summary>
		///     Dispatches the move to the appropriate executor based on its type.
		/// </summary>
		private static void ExecuteMoveOnBoard(GameState state, Move move)
		{
			switch (move.Type)
			{
				case MoveType.Normal:
				case MoveType.Capture:
					NormalMoveExecutor.Execute(state, move);
					break;

				case MoveType.CastleKingside:
				case MoveType.CastleQueenside:
					CastleMoveExecutor.Execute(state, move);
					break;

				case MoveType.EnPassant:
					EnPassantMoveExecutor.Execute(state, move);
					break;

				case MoveType.PawnPromotion:
				case MoveType.PawnPromotionCapture:
					PawnPromotionMoveExecutor.Execute(state, move);
					break;

				default:
					// It's good practice to handle unexpected cases.
					throw new ArgumentOutOfRangeException(nameof(move), $"Unknown move type: {move.Type}");
			}
		}
	}
}
