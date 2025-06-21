using System;
using Bezoro.Chess.Domain.Functions.Moves.Execution;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves
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
			Piece movingPiece = state.Board.GetPiece(move.From);

			// Determine the en passant target square for the *next* state.
			// This is only set when a pawn makes a two-square advance.
			Position? newEnPassantTargetSquare = null;
			if (movingPiece.Type == PieceType.Pawn && Math.Abs(move.From.Row - move.To.Row) == 2)
			{
				// The target square is the one "behind" the pawn's destination.
				int behindRow = move.From.Row + (move.To.Row - move.From.Row) / 2;
				newEnPassantTargetSquare = new Position(behindRow, move.From.Col);
			}

			// Create a new game state
			var newState = new GameState
			{
				// Copy the current board
				Board = state.Board,

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
			newState = ApplyMoveToBoard(newState, move);

			return newState;
		}

		private static bool ShouldResetHalfMoveClock(GameState state, Move move)
		{
			// Half-move clock resets on pawn moves or captures
			if (move.Type is MoveType.CastleKingside or MoveType.CastleQueenside)
			{
				return false;
			}

			if (state.Board.GetPiece(move.From).Type == PieceType.Pawn)
			{
				return true;
			}

			if (state.Board.GetPiece(move.To).Type != default)
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
			Piece          movingPiece = state.Board.GetPiece(move.From);

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
		private static GameState ApplyMoveToBoard(in GameState state, in Move move)
		{
			GameState newState = state;
			switch (move.Type)
			{
				case MoveType.Normal:
				case MoveType.Capture:
					newState = newState with { Board = NormalMoveExecutor.Execute(state, move) };
					break;

				case MoveType.CastleKingside:
				case MoveType.CastleQueenside:
					newState = newState with { Board = CastleMoveExecutor.Execute(state, move) };
					break;

				case MoveType.EnPassant:
					newState = newState with { Board = EnPassantMoveExecutor.Execute(state, move) };
					break;

				case MoveType.PawnPromotion:
				case MoveType.PawnPromotionCapture:
					newState = newState with { Board = PawnPromotionMoveExecutor.Execute(state, move) };
					break;

				default:
					// It's good practice to handle unexpected cases.
					throw new ArgumentOutOfRangeException(nameof(move), $"Unknown move type: {move.Type}");
			}

			return newState;
		}
	}
}
