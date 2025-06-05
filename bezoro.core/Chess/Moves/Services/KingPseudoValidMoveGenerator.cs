using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Abstractions.Interfaces;
using Bezoro.Core.Chess.Board;
using Bezoro.Core.Chess.Common.Extensions;
using Bezoro.Core.Chess.Common.Helpers;
using Bezoro.Core.Chess.Game.Models;
using Bezoro.Core.Chess.Moves.Models;
using Bezoro.Core.Chess.Pieces.Models;

namespace Bezoro.Core.Chess.Moves.Services
{
	public class KingPseudoValidMoveGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			if (game  == null) throw new ArgumentNullException(nameof(game));
			if (piece == null) throw new ArgumentNullException(nameof(piece));

			var board = game.Board;
			var king  = EnsureKingPiece(board, piece);

			var from = board.GetPosition(king);
			if (from == null) yield break;

			// Standard King Moves (1 square in any direction)
			foreach (var (dx, dy) in DirectionVectors.KING)
			{
				var toFile = from.Value.Column + dx;
				var toRow  = from.Value.Row    + dy;

				if (board.IsInside(toFile, toRow))
				{
					var to = new BoardPosition(toFile, toRow);
					yield return new(from.Value, to, king.Color, king.GetPieceType());
				}
			}

			// Castling Moves
			// For pseudo-legal moves, we primarily check if the king and rook have moved.
			// More complex castling rules (path clear, not in/through check) are for full legal move validation.
			if (!king.HasMoved && !king.HasCastled) // King hasn't moved or already castled
			{
				// Determine rook starting files and king's castling destination files
				var kingSideRookFile      = board.Width - 1; // H-file (index 7 for 8x8)
				var queenSideRookFile     = 0;               // A-file (index 0)
				var kingSideCastleToFile  = from.Value.Column + 2;
				var queenSideCastleToFile = from.Value.Column - 2;
				var kingRank              = from.Value.Row; // King's current rank

				// King-side Castling
				var kingSideRookPos   = new BoardPosition(kingSideRookFile, kingRank);
				var kingSideRookPiece = board.GetPieceAt(kingSideRookPos);
				if (kingSideRookPiece is RookModel kingSideRook &&
					kingSideRook.Color == king.Color            &&
					!kingSideRook.HasMoved)
				{
					var toKingSide = new BoardPosition(kingSideCastleToFile, kingRank);
					yield return Move.CastleKingSide(from.Value, toKingSide, king.Color);
				}

				// Queen-side Castling
				var queenSideRookPos   = new BoardPosition(queenSideRookFile, kingRank);
				var queenSideRookPiece = board.GetPieceAt(queenSideRookPos);
				if (queenSideRookPiece is RookModel queenSideRook &&
					queenSideRook.Color == king.Color             &&
					!queenSideRook.HasMoved)
				{
					var toQueenSide = new BoardPosition(queenSideCastleToFile, kingRank);
					yield return Move.CastleQueenSide(from.Value, toQueenSide, king.Color);
				}
			}
		}

	#endregion

		private static KingModel EnsureKingPiece(IChessBoardModel board, IChessPieceModel piece)
		{
			if (board == null) throw new ArgumentNullException(nameof(board));
			if (piece == null) throw new ArgumentNullException(nameof(piece));
			if (piece is not KingModel king)
				throw new ArgumentException("Generator received a non-king piece.", nameof(piece));

			return king;
		}
	}
}
