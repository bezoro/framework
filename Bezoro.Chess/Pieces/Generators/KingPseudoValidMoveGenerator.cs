using System;
using System.Collections.Generic;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.Pieces.Generators
{
	/// <summary>
	///     Generates pseudo-valid moves for a king piece, including regular moves, captures,
	///     and castling. Checks for blocked paths during castling and captures.
	/// </summary>
	public sealed class KingPseudoValidMoveGenerator : IPseudoMoveGenerator
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

			if (piece is not KingModel king)
				throw new ArgumentException("Generator received a non-king piece.", nameof(piece));

			var board                = game.Board; // board is now guaranteed not null
			var nullableFromPosition = board.GetPosition(king);
			if (nullableFromPosition == null)
				throw new ArgumentException("Generator received a king that is not on the board.", nameof(piece));

			var fromPosition = nullableFromPosition.Value;

			foreach (var move in GenerateMoves(game, king, fromPosition))
			{
				yield return move;
			}
		}

	#endregion

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

		private static IEnumerable<Move> GenerateMoves(GameModel game, KingModel king, BoardPosition from)
		{
			var board = game.Board;

			// Process regular king moves (including captures)
			foreach (var move in ProcessRegularMoves(king, from, board))
			{
				yield return move;
			}

			// Process castling moves if applicable
			if (!king.HasMoved && !king.HasCastled)
			{
				foreach (var move in ProcessKingsideCastling(king, from, board))
				{
					yield return move;
				}

				foreach (var move in ProcessQueensideCastling(king, from, board))
				{
					yield return move;
				}
			}
		}

		private static IEnumerable<Move> ProcessKingsideCastling(
			KingModel king,
			BoardPosition from,
			IChessBoardModel board)
		{
			// Determine rook starting files and king's castling destination files
			var kingSideRookFile     = board.Width - 1; // H-file (index 7 for 8x8)
			var kingSideCastleToFile = from.Column + 2;
			var kingRank             = from.Row; // King's current rank

			// Check if kingside rook is in place and hasn't moved
			var kingSideRookPos   = new BoardPosition(kingSideRookFile, kingRank);
			var kingSideRookPiece = board.GetPieceAt(kingSideRookPos);

			if (kingSideRookPiece is not RookModel kingSideRook ||
				kingSideRook.Color != king.Color                ||
				kingSideRook.HasMoved)
			{
				yield break;
			}

			// Check if path between king and rook is clear
			for (var file = from.Column + 1 ; file < kingSideRookFile ; file++)
			{
				var pos = new BoardPosition(file, kingRank);
				if (board.GetPieceAt(pos) != null)
				{
					yield break;
				}
			}

			// Path is clear, return the castling move
			var toKingSide = new BoardPosition(kingSideCastleToFile, kingRank);
			yield return Move.CastleKingSide(from, toKingSide, king.Color);
		}

		private static IEnumerable<Move> ProcessQueensideCastling(
			KingModel king,
			BoardPosition from,
			IChessBoardModel board)
		{
			// Determine rook starting files and king's castling destination files
			var queenSideRookFile     = 0; // A-file (index 0)
			var queenSideCastleToFile = from.Column - 2;
			var kingRank              = from.Row; // King's current rank

			// Check if queenside rook is in place and hasn't moved
			var queenSideRookPos   = new BoardPosition(queenSideRookFile, kingRank);
			var queenSideRookPiece = board.GetPieceAt(queenSideRookPos);

			if (queenSideRookPiece is not RookModel queenSideRook ||
				queenSideRook.Color != king.Color                 ||
				queenSideRook.HasMoved)
			{
				yield break;
			}

			// Check if path between king and rook is clear
			for (var file = from.Column - 1 ; file > queenSideRookFile ; file--)
			{
				var pos = new BoardPosition(file, kingRank);
				if (board.GetPieceAt(pos) != null)
				{
					yield break;
				}
			}

			// Path is clear, return the castling move
			var toQueenSide = new BoardPosition(queenSideCastleToFile, kingRank);
			yield return Move.CastleQueenSide(from, toQueenSide, king.Color);
		}

		private static IEnumerable<Move> ProcessRegularMoves(KingModel king, BoardPosition from, IChessBoardModel board)
		{
			foreach (var (dx, dy) in DirectionVectors.KING)
			{
				if (!TryGetValidTargetPosition(from, dx, dy, board, out var to))
					continue;

				var pieceAtDestination = board.GetPieceAt(to);

				// Skip if the destination square contains a piece of the same color
				if (pieceAtDestination != null && pieceAtDestination.Color == king.Color)
					continue;

				// If destination has an opponent's piece, create a capture move
				if (pieceAtDestination != null && pieceAtDestination.Color != king.Color)
				{
					yield return new(from, to, king.Color, king.GetPieceType(), MoveKind.Capture);
				}
				else // Empty square - normal move
				{
					yield return new(from, to, king.Color, king.GetPieceType());
				}
			}
		}
	}
}
