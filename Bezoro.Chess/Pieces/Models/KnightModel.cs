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
	public class KnightModel : PieceModel
	{
		public KnightModel(PlayerColor color) : base(color, new KnightPseudoValidMovesGenerator()) { }
	}

	public class KnightPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

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

	#region Helper Methods

		private static void ValidateParameters(GameModel game, IChessPieceModel piece)
		{
			if (game  == null) throw new ArgumentNullException(nameof(game));
			if (piece == null) throw new ArgumentNullException(nameof(piece));
		}

		private static BoardPosition? GetPiecePosition(IChessBoardModel board, IChessPieceModel piece) =>
			board.GetPosition(piece);

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

		private static (int file, int rank) CalculateTargetPosition(BoardPosition position, int dx, int dy) =>
			(position.Column + dx, position.Rank + dy);

		private static bool IsValidTargetPosition(IChessBoardModel board, IChessPieceModel piece, int file, int rank) =>
			board.IsInside(file, rank);

		private static bool IsOccupiedByFriendlyPiece(IChessPieceModel targetPiece, PlayerColor pieceColor) =>
			targetPiece != null && targetPiece.Color == pieceColor;

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

	#endregion
	}
}
