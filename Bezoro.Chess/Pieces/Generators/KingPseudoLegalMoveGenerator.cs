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
	public sealed class KingPseudoLegalMoveGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		/// <summary>
		///     Generates all pseudo-legal moves for a king piece in the current game position.
		/// </summary>
		/// <param name="game">The current game state.</param>
		/// <param name="piece">The king piece to generate moves for.</param>
		/// <returns>A collection of possible moves for the king.</returns>
		/// <exception cref="ArgumentNullException">Thrown when game or piece is null.</exception>
		/// <exception cref="ArgumentException">Thrown when piece is not a king or board is null.</exception>
		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			if (game is null) throw new ArgumentNullException(nameof(game));
			if (piece is null) throw new ArgumentNullException(nameof(piece));

			var board = game.Board ?? throw new ArgumentException("Game.Board cannot be null.", nameof(game));
			var king  = EnsureKingPiece(piece);

			var from = board.GetPosition(king);
			if (from is null) throw new ArgumentException("King is not on the board.", nameof(piece));

			foreach (var move in GenerateMoves(game, king, from.Value))
			{
				yield return move;
			}
		}

	#endregion

		/// <summary>
		///     Generates all possible moves for a king from the given position.
		///     Includes regular moves and castling if applicable.
		/// </summary>
		/// <param name="game">The current game state.</param>
		/// <param name="king">The king piece.</param>
		/// <param name="from">The current position of the king.</param>
		/// <returns>A collection of valid moves for the king.</returns>
		private static IEnumerable<Move> GenerateMoves(GameModel game, KingModel king, BoardPosition from)
		{
			var board = game.Board;

			// Process regular king moves (including captures)
			foreach (var move in GenerateRegularMoves(king, from, board))
			{
				yield return move;
			}

			// Process castling moves if applicable
			if (!king.HasMoved && !king.HasCastled)
			{
				// Try kingside castling
				if (TryGenerateKingsideCastling(king, from, board) is { } kingsideCastlingMove)
					yield return kingsideCastlingMove;

				// Try queenside castling
				if (TryGenerateQueensideCastling(king, from, board) is { } queensideCastlingMove)
					yield return queensideCastlingMove;
			}
		}

		/// <summary>
		///     Generates regular king moves using direction vectors.
		/// </summary>
		/// <param name="king">The king piece.</param>
		/// <param name="from">The current position of the king.</param>
		/// <param name="board">The chess board.</param>
		/// <returns>Collection of valid regular moves for the king.</returns>
		private static IEnumerable<Move> GenerateRegularMoves(
			KingModel king,
			BoardPosition from,
			IChessBoardModel board)
		{
			foreach (var (dx, dy) in DirectionVectors.KING)
			{
				var targetFile = (int)(from.Column + dx);
				var targetRank = (int)(from.Row    + dy);

				// Skip if position is outside the board
				if (!board.IsInside(targetFile, targetRank))
					continue;

				var to          = new BoardPosition(targetFile, targetRank);
				var targetPiece = board.GetPieceAt(to);

				// Skip if the destination square contains a friendly piece
				if (targetPiece != null && targetPiece.Color == king.Color)
					continue;

				// Create appropriate move type (capture or normal)
				var moveKind = targetPiece != null ? MoveKind.Capture : MoveKind.Normal;
				yield return Move.Standard(from, to, king.Color, king.GetPieceType(), moveKind);
				;
			}
		}

		/// <summary>
		///     Validates that the piece is a king.
		/// </summary>
		/// <param name="piece">The chess piece to validate.</param>
		/// <returns>The validated king piece.</returns>
		/// <exception cref="ArgumentException">Thrown when piece is not a king.</exception>
		private static KingModel EnsureKingPiece(IChessPieceModel piece)
		{
			if (piece is not KingModel king)
				throw new ArgumentException("Generator received a non-king piece.", nameof(piece));

			return king;
		}

		/// <summary>
		///     Attempts to generate a kingside castling move if conditions permit.
		/// </summary>
		/// <param name="king">The king piece.</param>
		/// <param name="from">The current position of the king.</param>
		/// <param name="board">The chess board.</param>
		/// <returns>A kingside castling move if valid, null otherwise.</returns>
		private static Move? TryGenerateKingsideCastling(KingModel king, BoardPosition from, IChessBoardModel board)
		{
			// Determine rook starting file and king's castling destination file
			var kingSideRookFile     = board.Width - 1; // H-file (index 7 for 8x8)
			var kingSideCastleToFile = from.Column + 2;
			var kingRank             = from.Row;

			// Check if kingside rook is in place and hasn't moved
			var kingSideRookPos   = new BoardPosition(kingSideRookFile, kingRank);
			var kingSideRookPiece = board.GetPieceAt(kingSideRookPos);

			if (kingSideRookPiece is not RookModel kingSideRook ||
				kingSideRook.Color != king.Color                ||
				kingSideRook.HasMoved)
			{
				return null;
			}

			// Check if path between king and rook is clear
			for (var file = from.Column + 1 ; file < kingSideRookFile ; file++)
			{
				if (board.GetPieceAt(new BoardPosition(file, kingRank)) != null)
					return null;
			}

			// Path is clear, return the castling move
			var toKingSide = new BoardPosition(kingSideCastleToFile, kingRank);
			return Move.CastleKingSide(from, toKingSide, king.Color);
		}

		/// <summary>
		///     Attempts to generate a queenside castling move if conditions permit.
		/// </summary>
		/// <param name="king">The king piece.</param>
		/// <param name="from">The current position of the king.</param>
		/// <param name="board">The chess board.</param>
		/// <returns>A queenside castling move if valid, null otherwise.</returns>
		private static Move? TryGenerateQueensideCastling(KingModel king, BoardPosition from, IChessBoardModel board)
		{
			// Determine rook starting file and king's castling destination file
			var queenSideRookColumn   = 0u; // A-file (index 0)
			var queenSideCastleToFile = from.Column - 2;
			var KingColumn            = from.Row;

			// Check if queenside rook is in place and hasn't moved
			var queenSideRookPos   = new BoardPosition(queenSideRookColumn, KingColumn);
			var queenSideRookPiece = board.GetPieceAt(queenSideRookPos);

			if (queenSideRookPiece is not RookModel queenSideRook ||
				queenSideRook.Color != king.Color                 ||
				queenSideRook.HasMoved)
			{
				return null;
			}

			// Check if path between king and rook is clear
			for (var file = from.Column - 1 ; file > queenSideRookColumn ; file--)
			{
				if (board.GetPieceAt(new BoardPosition(file, KingColumn)) != null)
					return null;
			}

			// Path is clear, return the castling move
			var toQueenSide = new BoardPosition(queenSideCastleToFile, KingColumn);
			return Move.CastleQueenSide(from, toQueenSide, king.Color);
		}
	}
}
