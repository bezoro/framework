using System;
using System.Collections.Generic;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.Moves.Services
{
	/// <summary>
	///     Emits pawn moves considering board occupancy and en-passant rules.
	///     The white template is mirrored for black by multiplying
	///     <c>dy</c> with <c>pawn.Direction</c> (+1 / −1).
	///     Check-legality is handled by higher layers.
	/// </summary>
	public sealed class PawnPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			if (game is null)
				throw new ArgumentNullException(nameof(game));

			var board = game.Board;

			if (board is null)
				throw new ArgumentNullException(nameof(board));

			if (piece is null)
				throw new ArgumentNullException(nameof(piece));

			if (piece is not PawnModel pawn)
				throw new ArgumentException("Generator received a non-pawn piece.", nameof(piece));

			var from = game.Board.GetPosition(pawn);

			if (from is null)
				throw new ArgumentException("Generator received a pawn on an invalid position.", nameof(piece));

			foreach (var mv in GenerateMoves(game, pawn, from.Value))
			{
				yield return mv;
			}
		}

	#endregion

		private static bool IsPromotionRank(PlayerColor color, int rank, IChessBoardModel board) =>
			color == PlayerColor.White ? rank == board.Height - 1 : rank == 0;

		private static IEnumerable<Move> BuildMoves(
			BoardPosition from,
			BoardPosition to,
			PawnModel pawn,
			MoveKind kind,
			IChessBoardModel board)
		{
			// Check for promotion
			if (kind != MoveKind.EnPassant && IsPromotionRank(pawn.Color, to.Row, board))
			{
				foreach (PromotionPieceType promo in Enum.GetValues(typeof(PromotionPieceType)))
				{
					if (promo != PromotionPieceType.None)
						yield return Move.Promotion(from, to, pawn.Color, promo);
				}
			}
			// Normal move
			else
				yield return new(from, to, pawn.Color, pawn.GetPieceType(), kind);
		}

		private static IEnumerable<Move> GenerateMoves(GameModel game, PawnModel pawn, BoardPosition from)
		{
			var board = game.Board;
			var dir   = pawn.Direction; // Pawn's forward direction (+1 for White, -1 for Black)

			// Single Push
			foreach (var move in ProcessSinglePush(pawn, from, board, dir))
			{
				yield return move;
			}

			// Double Push
			if (!pawn.HasMoved) // Optimization: Only attempt double push if pawn hasn't moved
			{
				foreach (var move in ProcessDoublePush(pawn, from, board, dir))
				{
					yield return move;
				}
			}

			// Captures (Left and Right)
			foreach (var move in ProcessCapture(pawn, from, board, dir, -1)) // Capture left
			{
				yield return move;
			}

			foreach (var move in ProcessCapture(pawn, from, board, dir, +1)) // Capture right
			{
				yield return move;
			}

			// En Passant (Left and Right)
			// En passant requires the game state for EnPassantTargetSquare
			foreach (var move in ProcessEnPassant(game, pawn, from, board, dir, -1)) // En Passant left
			{
				yield return move;
			}

			foreach (var move in ProcessEnPassant(game, pawn, from, board, dir, +1)) // En Passant right
			{
				yield return move;
			}
		}

		private static IEnumerable<Move> ProcessCapture(
			PawnModel pawn,
			BoardPosition from,
			IChessBoardModel board,
			int pawnDir,
			int captureDx)
		{
			const int dy = 1;

			var toFile = from.Column + captureDx;
			var toRank = from.Row    + dy * pawnDir;

			if (!board.IsInside(toFile, toRank))
				yield break;

			var toPos           = new BoardPosition(toFile, toRank);
			var pieceOnToSquare = board.Squares[toFile, toRank].Piece;

			if (pieceOnToSquare != null && pieceOnToSquare.Color != pawn.Color)
			{
				foreach (var move in BuildMoves(from, toPos, pawn, MoveKind.Capture, board))
				{
					yield return move;
				}
			}
		}

		private static IEnumerable<Move> ProcessDoublePush(
			PawnModel pawn,
			BoardPosition from,
			IChessBoardModel board,
			int pawnDir)
		{
			// Note: pawn.HasMoved check is now done in GenerateMoves before calling this.
			const int dx = 0;
			const int dy = 2; // Double step

			var toFile = from.Column + dx;
			var toRank = from.Row    + dy * pawnDir;

			if (!board.IsInside(toFile, toRank))
				yield break;

			var toPos           = new BoardPosition(toFile, toRank);
			var pieceOnToSquare = board.Squares[toFile, toRank].Piece;

			if (pieceOnToSquare == null) // Target square must be empty
			{
				// Check intermediate square (the one pawn jumps over)
				var intermediateRank          = from.Row + 1 * pawnDir;
				var pieceOnIntermediateSquare = board.Squares[from.Column, intermediateRank].Piece;

				if (pieceOnIntermediateSquare == null) // Intermediate square must also be empty
				{
					foreach (var move in BuildMoves(from, toPos, pawn, MoveKind.Normal, board))
					{
						yield return move;
					}
				}
			}
		}

		private static IEnumerable<Move> ProcessEnPassant(
			GameModel game,
			PawnModel pawn,
			BoardPosition from,
			IChessBoardModel board,
			int pawnDir,
			int epDx)
		{
			const int dy = 1;

			var toFile = from.Column + epDx;
			var toRank = from.Row    + dy * pawnDir;

			if (!board.IsInside(toFile, toRank))
				yield break;

			var toPos = new BoardPosition(toFile, toRank);

			if (IsPromotionRank(pawn.Color, toRank, board)) // En passant cannot result in promotion
				yield break;

			var pieceOnToSquare = board.Squares[toFile, toRank].Piece;

			if (pieceOnToSquare             == null &&
				board.EnPassantTargetSquare != null &&
				board.EnPassantTargetSquare.Position.Equals(toPos))
			{
				var capturedPawnFile = toPos.Column;
				var capturedPawnRank = from.Row;
				var capturedPawn     = board.Squares[capturedPawnFile, capturedPawnRank].Piece;

				if (capturedPawn                != null                &&
					capturedPawn.GetPieceType() == ChessPieceType.Pawn &&
					capturedPawn.Color          != pawn.Color)
				{
					foreach (var move in BuildMoves(from, toPos, pawn, MoveKind.EnPassant, board))
					{
						yield return move;
					}
				}
			}
		}

		private static IEnumerable<Move> ProcessSinglePush(
			PawnModel pawn,
			BoardPosition from,
			IChessBoardModel board,
			int pawnDir)
		{
			const int dx = 0;
			const int dy = 1;

			var toFile = from.Column + dx;
			var toRank = from.Row    + dy * pawnDir;

			if (!board.IsInside(toFile, toRank))
				yield break;

			var toPos    = new BoardPosition(toFile, toRank);
			var toSquare = board.Squares[toFile, toRank];

			if (toSquare.IsOccupied)
				yield break;

			foreach (var move in BuildMoves(from, toPos, pawn, MoveKind.Normal, board))
			{
				yield return move;
			}
		}
	}
}
