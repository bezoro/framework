using System;

namespace Bezoro.Chess.ChessLogic
{
	/// <summary>
	///     Responsible for applying moves to a game state and producing a new, updated game state.
	/// </summary>
	public static class MoveExecution
	{
		/// <summary>
		///     Executes a move and returns a new GameState that reflects the move's result.
		/// </summary>
		public static GameState ExecuteMove(GameState currentState, Move move)
		{
			// Get the piece that's moving
			var movingPiece = currentState.PiecePositions[move.From.Row, move.From.Col];

			// Determine the en passant target square for the *next* state.
			// This is only set when a pawn makes a two-square advance.
			Position? newEnPassantTargetSquare = null;
			if (movingPiece.Type == PieceType.Pawn && Math.Abs(move.From.Row - move.To.Row) == 2)
			{
				// The target square is the one "behind" the pawn's destination.
				var behindRow = move.From.Row + (move.To.Row - move.From.Row) / 2;
				newEnPassantTargetSquare = new Position(behindRow, move.From.Col);
			}

			// Create a new game state (we're using an immutable approach)
			var newState = new GameState
			{
				// Copy the current board
				PiecePositions = (Piece[,])currentState.PiecePositions.Clone(),

				// Switch the active color
				ActiveColor = currentState.ActiveColor == PieceColor.White ? PieceColor.Black : PieceColor.White,

				// Update the move counters
				HalfMoveClock = ShouldResetHalfMoveClock(currentState, move) ? 0 : currentState.HalfMoveClock + 1,
				FullMoveNumber = currentState.ActiveColor == PieceColor.Black
					? currentState.FullMoveNumber + 1
					: currentState.FullMoveNumber,

				// These will be updated as needed below
				Castling              = UpdateCastlingRights(currentState, move),
				EnPassantTargetSquare = newEnPassantTargetSquare
			};

			// Handle special move types
			switch (move.Type)
			{
				case MoveType.Normal:
					ExecuteNormalMove(newState, move);
					break;

				case MoveType.CastleKingside:
					ExecuteCastlingMove(newState, move, true);
					break;

				case MoveType.CastleQueenside:
					ExecuteCastlingMove(newState, move, false);
					break;

				case MoveType.EnPassant:
					ExecuteEnPassantMove(newState, move);
					break;

				case MoveType.PawnPromotion:
					ExecutePawnPromotionMove(newState, move);
					break;
			}

			return newState;
		}

		private static bool ShouldResetHalfMoveClock(GameState state, Move move)
		{
			// Half-move clock resets on pawn moves or captures
			if (state.PiecePositions[move.From.Row, move.From.Col].Type == PieceType.Pawn)
				return true;

			if (state.PiecePositions[move.To.Row, move.To.Col].Type != default)
				return true;

			if (move.Type == MoveType.EnPassant)
				return true;

			return false;
		}

		private static CastlingRights UpdateCastlingRights(GameState state, Move move)
		{
			var newRights = state.Castling;

			// King moves remove all castling rights for that color
			if (state.PiecePositions[move.From.Row, move.From.Col].Type == PieceType.King)
			{
				if (state.ActiveColor == PieceColor.White)
				{
					newRights &= ~CastlingRights.WhiteBoth;
				}
				else
				{
					newRights &= ~CastlingRights.BlackBoth;
				}
			}

			// Rook moves remove the castling right for that side
			if (state.PiecePositions[move.From.Row, move.From.Col].Type == PieceType.Rook)
			{
				if (state.ActiveColor == PieceColor.White)
				{
					if (move.From.Col == 0) newRights &= ~CastlingRights.WhiteQueenside;
					if (move.From.Col == 7) newRights &= ~CastlingRights.WhiteKingside;
				}
				else
				{
					if (move.From.Col == 0) newRights &= ~CastlingRights.BlackQueenside;
					if (move.From.Col == 7) newRights &= ~CastlingRights.BlackKingside;
				}
			}

			// If a rook is captured, remove the corresponding castling right
			if (move.To.Row == 0 && move.To.Col == 0) newRights &= ~CastlingRights.BlackQueenside;
			if (move.To.Row == 0 && move.To.Col == 7) newRights &= ~CastlingRights.BlackKingside;
			if (move.To.Row == 7 && move.To.Col == 0) newRights &= ~CastlingRights.WhiteQueenside;
			if (move.To.Row == 7 && move.To.Col == 7) newRights &= ~CastlingRights.WhiteKingside;

			return newRights;
		}

		private static void ExecuteCastlingMove(GameState state, Move move, bool isKingside)
		{
			// Move the king
			ExecuteNormalMove(state, move);

			// Also move the rook
			var rookFromCol = isKingside ? 7 : 0;
			var rookToCol   = isKingside ? 5 : 3;
			var row         = move.From.Row; // Same row as the king

			var rookMove = new Move(
				new(row, rookFromCol),
				new(row, rookToCol)
			);

			ExecuteNormalMove(state, rookMove);
		}

		private static void ExecuteEnPassantMove(GameState state, Move move)
		{
			// Move the pawn to the destination square
			ExecuteNormalMove(state, move);

			// Remove the captured pawn
			var capturedPawnRow = move.From.Row;
			var capturedPawnCol = move.To.Col;
			state.PiecePositions[capturedPawnRow, capturedPawnCol] = default;
		}

		private static void ExecuteNormalMove(GameState state, Move move)
		{
			// Simply move the piece from the source to the destination
			state.PiecePositions[move.To.Row, move.To.Col]     = state.PiecePositions[move.From.Row, move.From.Col];
			state.PiecePositions[move.From.Row, move.From.Col] = default; // Empty the source square
		}

		private static void ExecutePawnPromotionMove(GameState state, Move move)
		{
			// Move the pawn
			ExecuteNormalMove(state, move);

			// Replace it with a queen (for now, could make this configurable)
			state.PiecePositions[move.To.Row, move.To.Col] = new(
				PieceType.Queen,
				state.ActiveColor == PieceColor.White ? PieceColor.Black : PieceColor.White
			);
		}
	}
}
