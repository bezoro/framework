using System;
using System.Collections.Generic;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.Pieces.Generators
{
	/// <summary>
	///     Emits pawn moves considering board occupancy and en-passant rules.
	///     The white template is mirrored for black by multiplying
	///     <c>dy</c> with <c>pawn.Direction</c> (+1 / −1).
	///     Higher layers handle check-legality.
	/// </summary>
	public sealed class PawnPseudoLegalMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			if (game == null)
				throw new ArgumentNullException(nameof(game));

			if (game.Board == null)
				throw new ArgumentException("Game.Board cannot be null.", nameof(game));

			if (piece == null)
				throw new ArgumentNullException(nameof(piece));

			if (piece is not PawnModel pawn)
				throw new ArgumentException("Generator received a non-pawn piece.", nameof(piece));

			var board                = game.Board; // board is now guaranteed not null
			var nullableFromPosition = board.GetPosition(pawn);
			if (nullableFromPosition == null)
				throw new ArgumentException("Generator received a pawn that is not on the board.", nameof(piece));

			var fromPosition = nullableFromPosition.Value;

			foreach (var mv in GenerateMoves(game, pawn, fromPosition))
			{
				yield return mv;
			}
		}

	#endregion

		private static bool IsPromotionRank(PlayerColor color, int rank, IChessBoardModel board) =>
			color == PlayerColor.White ? rank == board.Height - 1 : rank == 0;

		private static bool TryGetValidTargetPosition(
			BoardPosition from,
			int deltaX,
			int deltaY,
			IChessBoardModel board,
			out BoardPosition targetPosition)
		{
			var toFile = from.Column + deltaX;
			var toRank = from.Row    + deltaY;

			if (!board.IsInside(toFile, toRank))
			{
				targetPosition = default;
				return false;
			}

			targetPosition = new(toFile, toRank);
			return true;
		}

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
			{
				yield return new(from, to, pawn.Color, pawn.GetPieceType(), kind);
			}
		}

		private static IEnumerable<Move> GenerateMoves(GameModel game, PawnModel pawn, BoardPosition from)
		{
			var board         = game.Board;
			var pawnDirection = pawn.Direction; // Pawn's forward direction (+1 for White, -1 for Black)

			// Single Push
			foreach (var move in ProcessSinglePush(pawn, from, board, pawnDirection))
			{
				yield return move;
			}

			// Double Push
			if (!pawn.HasMoved)
			{
				foreach (var move in ProcessDoublePush(pawn, from, board, pawnDirection))
				{
					yield return move;
				}
			}

			// Captures (Left and Right)
			foreach (var move in ProcessCapture(pawn, from, board, pawnDirection, -1)) // Capture left
			{
				yield return move;
			}

			foreach (var move in ProcessCapture(pawn, from, board, pawnDirection, +1)) // Capture right
			{
				yield return move;
			}

			// En Passant (Left and Right)
			foreach (var move in ProcessEnPassant(pawn, from, board, pawnDirection, -1)) // En Passant left
			{
				yield return move;
			}

			foreach (var move in ProcessEnPassant(pawn, from, board, pawnDirection, +1)) // En Passant right
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
			if (!TryGetValidTargetPosition(from, captureDx, 1 * pawnDir, board, out var toPos))
				yield break;

			var pieceOnToSquare = board.Squares[toPos.Column, toPos.Row].Piece;
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
			if (!TryGetValidTargetPosition(from, 0, 2 * pawnDir, board, out var toPos))
				yield break;

			var pieceOnToSquare = board.Squares[toPos.Column, toPos.Row].Piece;
			if (pieceOnToSquare != null) // Target square must be empty
				yield break;

			// Check intermediate square (the one pawn jumps over)
			var intermediateRank = from.Row + 1 * pawnDir;
			// This square's validity is implied if toPos (2 steps away) is valid.
			var pieceOnIntermediateSquare = board.Squares[from.Column, intermediateRank].Piece;
			if (pieceOnIntermediateSquare != null) // Intermediate square must also be empty
				yield break;

			foreach (var move in BuildMoves(from, toPos, pawn, MoveKind.Normal, board))
			{
				yield return move;
			}
		}

		private static IEnumerable<Move> ProcessEnPassant(
			PawnModel pawn,
			BoardPosition from,
			IChessBoardModel board,
			int pawnDir,
			int epDx)
		{
			if (!TryGetValidTargetPosition(from, epDx, 1 * pawnDir, board, out var toPos))
				yield break;

			if (IsPromotionRank(pawn.Color, toPos.Row, board)) // En passant cannot result in promotion
				yield break;

			var pieceOnLandingSquare = board.Squares[toPos.Column, toPos.Row].Piece;
			if (pieceOnLandingSquare != null) // Landing square for en passant must be empty
				yield break;

			if (board.EnPassantTargetSquare == null || !board.EnPassantTargetSquare.Position.Equals(toPos))
			{
				yield break;
			}

			var capturedPawnFile = toPos.Column;
			var capturedPawnRank = from.Row; // Captured pawn is on the same rank as the attacking pawn
			// This position's validity (from.Column + epDx, from.Row) is implied if toPos is valid.
			var capturedPawn = board.Squares[capturedPawnFile, capturedPawnRank].Piece;
			if (capturedPawn                == null                ||
				capturedPawn.GetPieceType() != ChessPieceType.Pawn ||
				capturedPawn.Color          == pawn.Color)
				yield break;

			foreach (var move in BuildMoves(from, toPos, pawn, MoveKind.EnPassant, board))
			{
				yield return move;
			}
		}

		private static IEnumerable<Move> ProcessSinglePush(
			PawnModel pawn,
			BoardPosition from,
			IChessBoardModel board,
			int pawnDir)
		{
			if (!TryGetValidTargetPosition(from, 0, 1 * pawnDir, board, out var toPos))
				yield break;

			var toSquare = board.Squares[toPos.Column, toPos.Row];
			if (toSquare.IsOccupied)
				yield break;

			foreach (var move in BuildMoves(from, toPos, pawn, MoveKind.Normal, board))
			{
				yield return move;
			}
		}
	}
}
