using System;
using System.Collections.Generic;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Board.Models;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Moves.Models;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.Common.Helpers
{
	public static class ChessUtils
	{
		public static readonly Dictionary<(PlayerColor color, CastleSide side),
			(BoardPosition kingFrom, BoardPosition kingTo,
			BoardPosition rookFrom, BoardPosition rookTo)> CastlePositions = new()
		{
			{ (PlayerColor.White, CastleSide.King), (new("e1"), new("g1"), new("h1"), new("f1")) },
			{ (PlayerColor.White, CastleSide.Queen), (new("e1"), new("c1"), new("a1"), new("d1")) },
			{ (PlayerColor.Black, CastleSide.King), (new("e8"), new("g8"), new("h8"), new("f8")) },
			{ (PlayerColor.Black, CastleSide.Queen), (new("e8"), new("c8"), new("a8"), new("d8")) }
		};

		public static char GetCharFromPiece(IChessPieceModel piece)
		{
			var pieceChar = piece.GetType().Name switch
			{
				nameof(KingModel)   => 'k',
				nameof(QueenModel)  => 'q',
				nameof(RookModel)   => 'r',
				nameof(BishopModel) => 'b',
				nameof(KnightModel) => 'n',
				nameof(PawnModel)   => 'p',
				_                   => throw new ArgumentException($"Invalid piece type: {piece.GetType().Name}")
			};

			return piece.Color == PlayerColor.White ? char.ToUpper(pieceChar) : pieceChar;
		}

		/// <summary>
		///     Converts a FEN (Forsyth–Edwards Notation) character to its corresponding chess piece type.
		/// </summary>
		/// <param name="fenChar">The FEN character representing a chess piece.</param>
		/// <returns>The corresponding ChessPieceType.</returns>
		/// <exception cref="ArgumentException">Thrown when the FEN character is invalid.</exception>
		public static ChessPieceType GetPieceTypeFromChar(char fenChar)
		{
			switch (fenChar)
			{
				case 'k': return ChessPieceType.King;
				case 'q': return ChessPieceType.Queen;
				case 'r': return ChessPieceType.Rook;
				case 'b': return ChessPieceType.Bishop;
				case 'n': return ChessPieceType.Knight;
				case 'p': return ChessPieceType.Pawn;
				default:  throw new ArgumentException($"Invalid FEN piece character: {fenChar}");
			}
		}

		/// <summary>
		///     Creates a chess piece model from a FEN (Forsyth–Edwards Notation) character.
		/// </summary>
		/// <param name="fenChar">The FEN character representing a chess piece.</param>
		/// <returns>A new instance of IChessPieceModel representing the chess piece.</returns>
		/// <exception cref="ArgumentException">Thrown when the FEN character is invalid.</exception>
		public static IChessPieceModel GetPieceFromChar(char fenChar)
		{
			var color     = fenChar.ToPlayerColor();
			var lowerChar = char.ToLower(fenChar);

			return lowerChar switch
			{
				'k' => new KingModel(color),
				'p' => new PawnModel(color),
				'q' => new QueenModel(color),
				'r' => new RookModel(color),
				'b' => new BishopModel(color),
				'n' => new KnightModel(color),
				_   => throw new ArgumentException($"Invalid FEN piece character: {fenChar}")
			};
		}

		/// <summary>
		///     Generates all possible sliding moves for a piece in the given directions.
		/// </summary>
		/// <param name="board">The chess board model.</param>
		/// <param name="from">The starting square of the piece.</param>
		/// <param name="directions">Array of direction vectors (dx,dy) to check for moves.</param>
		/// <param name="movingSide">The color of the side to move.</param>
		/// <param name="movingPieceType">The type of piece being moved.</param>
		/// <param name="movingPieceColor">The color of the piece being moved.</param>
		/// <returns>An enumerable collection of valid moves for the piece.</returns>
		public static IEnumerable<Move> GenerateSlidingMoves(
			IChessBoardModel board,
			IChessBoardSquareModel from,
			(int dx, int dy)[] directions,
			PlayerColor movingSide,
			ChessPieceType movingPieceType,
			PlayerColor movingPieceColor)
		{
			foreach (var (dx, dy) in directions)
			{
				var x = from.Position.Column + dx;
				var y = from.Position.Row    + dy;

				while (board.IsInside(x, y))
				{
					var to = new BoardSquareModel(x, y);

					if (board.IsEmpty(to.Position))
					{
						yield return Move.Standard(
							from.Position, to.Position, movingSide, movingPieceType, MoveKind.Normal);
					}
					else
					{
						if (board.IsEnemy(to, movingPieceColor))
						{
							yield return Move.Standard(
								from.Position, to.Position, movingPieceColor, movingPieceType, MoveKind.Capture);
						}

						break; // blocked
					}

					x += dx; // step again
					y += dy;
				}
			}
		}

		/// <summary>
		///     Converts a character to a PlayerColor based on its case.
		/// </summary>
		/// <param name="c">The character to convert.</param>
		/// <returns>White for uppercase characters, Black for lowercase characters.</returns>
		public static PlayerColor ToPlayerColor(this char c) =>
			char.IsUpper(c) ? PlayerColor.White : PlayerColor.Black;
	}
}
