using System;
using System.Collections.Generic;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Pieces.Models
{
	/// <summary>
	///     Generates pseudo-legal moves for a knight chess piece.
	///     Pseudo-legal moves are all possible knight moves without considering if they put the king in check.
	/// </summary>
	public sealed class KnightPseudoLegalMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		/// <summary>
		///     Generates all pseudo-legal moves for a knight piece in the current game position.
		/// </summary>
		/// <param name="game">The current game state.</param>
		/// <param name="piece">The knight piece to generate moves for.</param>
		/// <returns>A collection of possible moves for the knight.</returns>
		/// <exception cref="ArgumentNullException">Thrown when game or piece is null.</exception>
		/// <exception cref="ArgumentException">Thrown when piece is not a knight.</exception>
		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			if (game is null) throw new ArgumentNullException(nameof(game));
			if (piece is null) throw new ArgumentNullException(nameof(piece));

			var board  = game.Board;
			var knight = EnsureKnightPiece(board, piece);

			var from = board.GetPosition(knight);
			if (from is null) yield break;

			foreach (var move in GenerateMoves(board, knight, from.Value))
			{
				yield return move;
			}
		}

	#endregion

		/// <summary>
		///     Generates all possible moves for a knight from the given position.
		///     Knights move in an L-shape pattern to any of 8 possible positions.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="knight">The knight piece.</param>
		/// <param name="from">The current position of the knight.</param>
		/// <returns>A collection of valid moves for the knight.</returns>
		private static IEnumerable<Move> GenerateMoves(
			IChessBoardModel board,
			KnightModel knight,
			BoardPosition from)
		{
			// Use DirectionVectors.KNIGHT for knight movement
			foreach (var (dx, dy) in DirectionVectors.KNIGHT)
			{
				// Calculate target position
				var targetFile = (int)(from.Column + dx);
				var targetRank = (int)(from.Row    + dy);

				// Skip if position is outside the board
				if (!board.IsInside(targetFile, targetRank))
					continue;

				// Get target square and piece
				var to           = new BoardPosition(targetFile, targetRank);
				var targetSquare = board.Squares[targetFile, targetRank];
				var targetPiece  = targetSquare.GetPiece();

				// Skip if target square contains a friendly piece
				if (targetPiece != null && targetPiece.Color == knight.Color)
					continue;

				// Create appropriate move type (capture or normal)
				var moveKind = targetPiece != null ? MoveKind.Capture : MoveKind.Normal;
				yield return Move.Standard(from, to, knight.Color, knight.GetPieceType(), moveKind);
				;
			}
		}

		/// <summary>
		///     Validates that the piece is a knight and the parameters are not null.
		/// </summary>
		/// <param name="board">The chess board to validate.</param>
		/// <param name="piece">The chess piece to validate.</param>
		/// <returns>The validated knight piece.</returns>
		/// <exception cref="ArgumentNullException">Thrown when board or piece is null.</exception>
		/// <exception cref="ArgumentException">Thrown when piece is not a knight.</exception>
		private static KnightModel EnsureKnightPiece(IChessBoardModel board, IChessPieceModel piece)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (piece is not KnightModel knight)
				throw new ArgumentException("Generator received a non-knight piece.", nameof(piece));

			return knight;
		}
	}
}
