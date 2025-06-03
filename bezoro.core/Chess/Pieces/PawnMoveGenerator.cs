using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	/// <summary>
	///     Generates all legal moves for a single pawn, including:
	///     • single push (+ promotion)
	///     • double push (from starting rank)
	///     • captures (+ promotion)
	///     • en-passant captures
	/// </summary>
	public sealed class PawnMoveGenerator : IMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(IChessBoardModel board, PieceModel piece)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (piece is not PawnModel pawn)
				throw new ArgumentException("Piece supplied to PawnMoveGenerator is not a pawn.", nameof(piece));

			var result = new List<Move>();

			//------------------------------------------------------------------
			// 1. Locate the pawn on the board
			//------------------------------------------------------------------
			var currentPos = board.GetPosition(pawn);
			if (currentPos is null) return Enumerable.Empty<Move>();

			var dir  = pawn.Direction; // +1 for white, ‑1 for black
			int file = currentPos.Value.File;
			var rank = currentPos.Value.Rank;

			//------------------------------------------------------------------
			// 2. Single push (and possible promotion)
			//------------------------------------------------------------------
			var oneAhead = new BoardPosition(file, rank + dir);
			if (IsEmpty(board, oneAhead))
			{
				AddMoveOrPromotion(pawn, currentPos.Value, oneAhead, MoveKind.Normal, result, board);

				//--------------------------------------------------------------
				// 3. Double push
				//--------------------------------------------------------------
				if (!pawn.HasMoved)
				{
					var twoAhead = new BoardPosition(file, rank + 2 * dir);
					if (IsEmpty(board, twoAhead))
						result.Add(new(currentPos.Value, twoAhead));
				}
			}

			//------------------------------------------------------------------
			// 4. Captures (diagonal left / right)
			//------------------------------------------------------------------
			AddCaptureIfValid(board, pawn, file - 1, rank + dir, result);
			AddCaptureIfValid(board, pawn, file + 1, rank + dir, result);

			//------------------------------------------------------------------
			// 5. En-passant
			//------------------------------------------------------------------
			AddEnPassantIfValid(board, pawn, file - 1, rank, dir, result);
			AddEnPassantIfValid(board, pawn, file + 1, rank, dir, result);

			return result;
		}

	#endregion

		private static bool IsEmpty(IChessBoardModel board, BoardPosition pos) =>
			IsInside(board, pos.File, pos.Rank) && board.Squares[pos.File, pos.Rank].GetPiece() is null;

		private static bool IsInside(IChessBoardModel board, int file, int rank) =>
			board.IsInside(file, rank);

		private static bool IsPromotionRank(PlayerColor color, int rank, IChessBoardModel board) =>
			color == PlayerColor.White ? rank == board.Height - 1 : rank == 0;

		private static void AddCaptureIfValid(
			IChessBoardModel board,
			PawnModel pawn,
			int targetFile,
			int targetRank,
			ICollection<Move> moves)
		{
			if (!IsInside(board, targetFile, targetRank)) return;

			var targetPiece = board.Squares[targetFile, targetRank].GetPiece();
			if (targetPiece is null || targetPiece.Color == pawn.Color) return;

			var from = board.GetPosition(pawn)!.Value;
			var to   = new BoardPosition(targetFile, targetRank);

			AddMoveOrPromotion(pawn, from, to, MoveKind.Capture, moves, board);
		}

		private static void AddEnPassantIfValid(
			IChessBoardModel board,
			PawnModel pawn,
			int adjacentFile,
			int rank,
			int dir,
			ICollection<Move> moves)
		{
			if (!IsInside(board, adjacentFile, rank)) return;

			var adjacentPawn = board.Squares[adjacentFile, rank].GetPiece() as PawnModel;
			if (adjacentPawn is null || adjacentPawn.Color == pawn.Color || !adjacentPawn.JustAdvancedTwoSquares)
				return;

			var from = board.GetPosition(pawn)!.Value;
			var to   = new BoardPosition(adjacentFile, rank + dir);

			if (IsEmpty(board, to))
				moves.Add(new(from, to, MoveKind.EnPassant));
		}

		private static void AddMoveOrPromotion(
			PawnModel pawn,
			BoardPosition from,
			BoardPosition to,
			MoveKind defaultKind,
			ICollection<Move> moves,
			IChessBoardModel board)
		{
			if (IsPromotionRank(pawn.Color, to.Rank, board))
			{
				foreach (PromotionPieceType p in Enum.GetValues(typeof(PromotionPieceType)))
				{
					moves.Add(Move.Promotion(from, to, p));
				}
			}
			else
			{
				moves.Add(new(from, to, defaultKind));
			}
		}
	}
}
