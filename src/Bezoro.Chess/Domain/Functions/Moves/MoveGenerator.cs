using System;
using System.Collections.Generic;
using Bezoro.Chess.Domain.Functions.Moves.Generation;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Functions.Moves
{
	/// <summary>
	///     A static utility class for generating all possible pseudo-legal moves for a given game state.
	///     It does not check for moves that would leave the king in check.
	/// </summary>
	internal static class MoveGenerator
	{
		/// <summary>
		///     Generates all moves for the currently active player.
		/// </summary>
		public static IEnumerable<Move> GenerateMoves(GameState gameState)
		{
			for (var r = 0 ; r < 8 ; r++)
			{
				for (var c = 0 ; c < 8 ; c++)
				{
					Piece piece = gameState.Board.GetPiece(new Position(r, c));

					// Skip empty squares or pieces of the inactive color
					if (piece.Type == PieceType.None || piece.Color != gameState.ActiveColor)
					{
						continue;
					}

					var from = new Position(r, c);
					foreach (Move move in GeneratePieceMoves(from, piece, gameState))
					{
						yield return move;
					}
				}
			}
		}

		/// <summary>
		///     Generates all valid moves for a specific piece at the given position.
		///     This can be used by the UI to show possible moves when a piece is selected.
		/// </summary>
		public static IEnumerable<Move> GeneratePieceMoves(Position from, GameState gameState)
		{
			Piece piece = gameState.Board.GetPiece(from);

			// Can't move an empty square or a piece of the wrong color
			if (piece.Type == PieceType.None || piece.Color != gameState.ActiveColor)
			{
				yield break;
			}

			foreach (Move move in GeneratePieceMoves(from, piece, gameState))
			{
				yield return move;
			}
		}

		private static IEnumerable<Move> GeneratePieceMoves(Position from, Piece piece, GameState gameState) =>
			piece.Type switch
			{
				PieceType.Pawn   => PawnMoveGenerator.GenerateMoves(from, gameState),
				PieceType.Knight => KnightMoveGenerator.GenerateMoves(from, gameState),
				PieceType.Bishop => BishopMoveGenerator.GenerateMoves(from, gameState),
				PieceType.Rook   => RookMoveGenerator.GenerateMoves(from, gameState),
				PieceType.Queen  => QueenMoveGenerator.GenerateMoves(from, gameState),
				PieceType.King   => KingMoveGenerator.GenerateMoves(from, gameState),
				_                => Array.Empty<Move>()
			};
	}
}
