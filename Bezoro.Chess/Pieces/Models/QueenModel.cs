using System;
using System.Collections.Generic;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Pieces.Models
{
	public class QueenModel : PieceModel
	{
		public QueenModel(PlayerColor color) : base(color, new QueenPseudoValidMovesGenerator()) { }
	}

	public class QueenPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			if (game is null) throw new ArgumentNullException(nameof(game));

			var board = game.Board;
			var queen = EnsureQueenPiece(board, piece);

			var from = board.GetPosition(queen);
			if (from is null) yield break;

			// Generate orthogonal moves (like a Rook)
			foreach (var square in board.GetOrthogonalSquares(from.Value))
			{
				var to = new BoardPosition(square.Position.Column, square.Position.Row);
				yield return new(from.Value, to, queen.Color, queen.GetPieceType());
			}

			// Generate diagonal moves (like a Bishop)
			foreach (var square in board.GetDiagonalSquares(from.Value))
			{
				var to = new BoardPosition(square.Position.Column, square.Position.Row);
				yield return new(from.Value, to, queen.Color, queen.GetPieceType());
			}
		}

	#endregion

		private static QueenModel EnsureQueenPiece(IChessBoardModel board, IChessPieceModel piece)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (piece is null) throw new ArgumentNullException(nameof(piece));
			if (piece is not QueenModel queen)
				throw new ArgumentException("Generator received a non-queen piece.", nameof(piece));

			return queen;
		}
	}
}
