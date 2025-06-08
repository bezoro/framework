using System;
using System.Collections.Generic;
using System.Linq;
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
	public class KnightPseudoLegalMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		/// <summary>
		///     Generates all pseudo-legal moves for a knight piece in the current game position.
		/// </summary>
		/// <param name="game">The current game state.</param>
		/// <param name="piece">The knight piece to generate moves for.</param>
		/// <returns>A collection of possible moves for the knight.</returns>
		/// <exception cref="ArgumentNullException">Thrown when game or piece is null.</exception>
		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			ValidateParameters(game, piece);

			var board    = game.Board;
			var position = GetPiecePosition(board, piece);

			if (position == null)
				return Enumerable.Empty<Move>();

			var moves = new List<Move>();

			GenerateKnightMoves(board, piece, position.Value, moves);

			return moves;
		}

	#endregion

		private static (int file, int rank) CalculateTargetPosition(BoardPosition position, int dx, int dy) =>
			(position.Column + dx, position.Rank + dy);

		private static BoardPosition? GetPiecePosition(IChessBoardModel board, IChessPieceModel piece) =>
			board.GetPosition(piece);

		private static bool IsOccupiedByFriendlyPiece(IChessPieceModel targetPiece, PlayerColor pieceColor) =>
			targetPiece != null && targetPiece.Color == pieceColor;

		private static bool IsValidTargetPosition(IChessBoardModel board, IChessPieceModel piece, int file, int rank) =>
			board.IsInside(file, rank);

		/// <summary>
		///     Creates a new Move instance for a knight move.
		/// </summary>
		/// <param name="origin">The starting position.</param>
		/// <param name="targetSquare">The target square.</param>
		/// <param name="pieceColor">The color of the moving piece.</param>
		/// <param name="targetPiece">The piece at the target square, if any.</param>
		/// <returns>A new Move instance representing the knight's move.</returns>
		private static Move CreateKnightMove(
			BoardPosition origin,
			IChessBoardSquareModel targetSquare,
			PlayerColor pieceColor,
			IChessPieceModel targetPiece)
		{
			var moveKind = targetPiece != null ? MoveKind.Capture : MoveKind.Normal;
			return new(
				origin,
				targetSquare.Position,
				pieceColor,
				ChessPieceType.Knight,
				moveKind);
		}

		/// <summary>
		///     Generates all possible knight moves from the given position.
		/// </summary>
		/// <param name="board">The chess board.</param>
		/// <param name="piece">The knight piece.</param>
		/// <param name="position">The current position of the knight.</param>
		/// <param name="moves">The list to add generated moves to.</param>
		private static void GenerateKnightMoves(
			IChessBoardModel board,
			IChessPieceModel piece,
			BoardPosition position,
			List<Move> moves)
		{
			foreach (var (dx, dy) in DirectionVectors.KNIGHT)
			{
				var targetPosition = CalculateTargetPosition(position, dx, dy);

				if (!IsValidTargetPosition(board, piece, targetPosition.file, targetPosition.rank))
					continue;

				var targetSquare = board.Squares[targetPosition.file, targetPosition.rank];
				var targetPiece  = targetSquare.GetPiece();

				if (IsOccupiedByFriendlyPiece(targetPiece, piece.Color))
					continue;

				moves.Add(CreateKnightMove(position, targetSquare, piece.Color, targetPiece));
			}
		}

		/// <summary>
		///     Validates that the required parameters are not null.
		/// </summary>
		/// <param name="game">The game model to validate.</param>
		/// <param name="piece">The chess piece to validate.</param>
		/// <exception cref="ArgumentNullException">Thrown when game or piece is null.</exception>
		private static void ValidateParameters(GameModel game, IChessPieceModel piece)
		{
			if (game  == null) throw new ArgumentNullException(nameof(game));
			if (piece == null) throw new ArgumentNullException(nameof(piece));
		}
	}
}
