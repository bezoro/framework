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
	/// <summary>
	///     Runtime representation of a rook on the chess board.
	/// </summary>
	public sealed class RookModel : PieceModel
	{
		public RookModel(PlayerColor color)
			: base(color, new RookPseudoValidMovesGenerator()) { }
	}

	/// <summary>
	///     Emits every geometrically legal (pseudo-legal) rook move.
	///     It deliberately ignores occupancy and self-check – higher-level
	///     validators will take care of that.
	/// </summary>
	public sealed class RookPseudoValidMovesGenerator : IPseudoMoveGenerator
	{
	#region Interface Implementations

		public IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece)
		{
			if (game is null) throw new ArgumentNullException(nameof(game));

			var board = game.Board;
			var rook  = EnsureRookPiece(board, piece);

			var from = board.GetPosition(rook);
			if (from is null) yield break;

			foreach (var move in GenerateMoves(board, rook, from.Value))
			{
				yield return move;
			}
		}

	#endregion

		private static IEnumerable<Move> GenerateMoves(
			IChessBoardModel board,
			RookModel rook,
			BoardPosition from)
		{
			foreach (var square in board.GetOrthogonalSquares(from))
			{
				var to = new BoardPosition(square.Position.Column, square.Position.Row);
				yield return new(from, to, rook.Color, rook.GetPieceType());
			}
		}

		private static RookModel EnsureRookPiece(IChessBoardModel board, IChessPieceModel piece)
		{
			if (board is null) throw new ArgumentNullException(nameof(board));
			if (piece is null) throw new ArgumentNullException(nameof(piece));
			if (piece is not RookModel rook)
				throw new ArgumentException("Generator received a non-rook piece.", nameof(piece));

			return rook;
		}
	}
}
