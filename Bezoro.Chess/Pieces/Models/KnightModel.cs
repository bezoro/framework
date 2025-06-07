using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Abstractions.Interfaces;
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
			if (game  == null) throw new ArgumentNullException(nameof(game));
			if (piece == null) throw new ArgumentNullException(nameof(piece));

			var board    = game.Board;
			var position = board.GetPosition(piece);

			if (position == null)
				return Enumerable.Empty<Move>();

			var moves = new List<Move>();

			foreach (var (dx, dy) in DirectionVectors.KNIGHT)
			{
				var targetFile = position.Value.Column + dx;
				var targetRank = position.Value.Rank   + dy;

				// Skip if the target is outside the board
				if (!board.IsInside(targetFile, targetRank))
					continue;

				var targetSquare = board.Squares[targetFile, targetRank];
				var targetPiece  = targetSquare.GetPiece();

				// Skip if occupied by friendly piece
				if (targetPiece != null && targetPiece.Color == piece.Color)
					continue;

				// Create move (normal or capture)
				var moveKind = targetPiece != null ? MoveKind.Capture : MoveKind.Normal;
				moves.Add(
					new(
						position.Value,
						targetSquare.Position,
						piece.Color,
						ChessPieceType.Knight,
						moveKind));
			}

			return moves;
		}

	#endregion
	}
}
