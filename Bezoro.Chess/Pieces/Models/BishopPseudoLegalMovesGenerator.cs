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
	///     Generates pseudo-legal moves for a bishop chess piece.
	///     Pseudo-legal moves are all possible moves without considering if they put the king in check.
	/// </summary>
	public sealed class BishopPseudoLegalMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		/// <summary>
		///     Generates all pseudo-legal moves for a bishop piece in the current game position.
		/// </summary>
		/// <param name="game">The current game state.</param>
		/// <param name="piece">The bishop piece to generate moves for.</param>
		/// <returns>A collection of possible moves for the bishop.</returns>
		/// <exception cref="ArgumentNullException">Thrown when game or piece is null.</exception>
		/// <exception cref="ArgumentException">Thrown when piece is not a bishop.</exception>
		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			if (game is null) throw new ArgumentNullException(nameof(game));

			var board  = game.Board;
			var bishop = EnsureBishopPiece(board, piece);

			var from = board.GetPosition(bishop);
			if (from is null) yield break;

			foreach (var move in GenerateMoves(board, bishop, from.Value))
			{
				yield return move;
			}
		}

	#endregion

		/// <summary>
		///     Validates that the piece is a bishop and the parameters are not null.
		/// </summary>
		/// <param name="board">The chess board to validate.</param>
		/// <param name="piece">The chess piece to validate.</param>
		/// <returns>The validated bishop piece.</returns>
		/// <exception cref="ArgumentNullException">Thrown when board or piece is null.</exception>
		/// <exception cref="ArgumentException">Thrown when piece is not a bishop.</exception>
		private static BishopModel EnsureBishopPiece(IChessBoardModel board, IChessPieceModel piece)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (piece is null) throw new ArgumentNullException(nameof(piece));
			if (piece is not BishopModel bishop)
				throw new ArgumentException("Generator received a non-bishop piece.", nameof(piece));

			return bishop;
		}

		/// <summary>
		///     Generates all possible moves for a bishop from the given position.
		///     Uses direction vectors to iterate through all diagonal directions efficiently.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="bishop">The bishop piece.</param>
		/// <param name="from">The current position of the bishop.</param>
		/// <returns>A collection of valid moves for the bishop.</returns>
		private static IEnumerable<Move> GenerateMoves(
			IChessBoardModel board,
			BishopModel bishop,
			BoardPosition from)
		{
			// Use DirectionVectors.DIAGONAL for bishop movement
			foreach (var (dx, dy) in DirectionVectors.DIAGONAL)
			{
				foreach (var move in GenerateMovesInDirection(board, bishop, from, dx, dy))
				{
					yield return move;
				}
			}
		}

		/// <summary>
		///     Generates moves in a specific direction using direction vectors.
		///     Continues in the direction until blocked by a piece or edge of board.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="bishop">The bishop piece.</param>
		/// <param name="from">The starting position.</param>
		/// <param name="dx">Horizontal direction offset.</param>
		/// <param name="dy">Vertical direction offset.</param>
		/// <returns>A collection of valid moves in the specified direction.</returns>
		private static IEnumerable<Move> GenerateMovesInDirection(
			IChessBoardModel board,
			BishopModel bishop,
			BoardPosition from,
			int dx,
			int dy)
		{
			var currentFile = from.Column + dx;
			var currentRank = from.Row    + dy;

			while (board.IsInside(currentFile, currentRank))
			{
				var to           = new BoardPosition(currentFile, currentRank);
				var targetSquare = board.Squares[currentFile, currentRank];
				var targetPiece  = targetSquare.GetPiece();

				// If square is occupied by friendly piece, we can't move there
				if (targetPiece != null && targetPiece.Color == bishop.Color)
					break;

				// Create appropriate move type (capture or normal)
				var moveKind = targetPiece != null ? MoveKind.Capture : MoveKind.Normal;
				yield return Move.Standard(from, to, bishop.Color, bishop.GetPieceType(), moveKind);

				// If this square contains any piece (friend or enemy), we can't move past it
				if (targetPiece != null)
					break;

				// Move to next square in this direction
				currentFile += dx;
				currentRank += dy;
			}
		}
	}
}
