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
	public class BishopModel : PieceModel
	{
		public BishopModel(PlayerColor color) : base(color, new BishopPseudoLegalMovesGenerator()) { }
	}

	public class BishopPseudoLegalMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

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

		private static BishopModel EnsureBishopPiece(IChessBoardModel board, IChessPieceModel piece)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (piece is null) throw new ArgumentNullException(nameof(piece));
			if (piece is not BishopModel bishop)
				throw new ArgumentException("Generator received a non-bishop piece.", nameof(piece));

			return bishop;
		}

		private static IEnumerable<Move> GenerateMoves(
			IChessBoardModel board,
			BishopModel bishop,
			BoardPosition from)
		{
			foreach (var square in board.GetDiagonalSquares(from))
			{
				var to = new BoardPosition(square.Position.Column, square.Position.Row);
				yield return new(from, to, bishop.Color, bishop.GetPieceType());
			}
		}
	}
}
