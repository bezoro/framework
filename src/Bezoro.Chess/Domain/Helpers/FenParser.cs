using System;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Helpers
{
	internal static class FenParser
	{
		private const int BoardSize = 8;

		public static GameState FenToGameState(string fen)
		{
			(string placement, string color, string castling, string enPassant, string halfMove, string fullMove) =
				SplitFenString(fen);

			return new GameState
			{
				PiecePositions        = ParsePiecePlacement(placement),
				ActiveColor           = ParseActiveColor(color),
				Castling              = ParseCastlingRights(castling),
				EnPassantTargetSquare = ParseEnPassantTarget(enPassant),
				HalfMoveClock         = ParseHalfMoveClock(halfMove),
				FullMoveNumber        = ParseFullMoveNumber(fullMove)
			};
		}

		private static (string placement, string color, string castling,
			string enPassant, string halfMove, string fullMove) SplitFenString(string fen)
		{
			string[]? parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);

			const int expectedFenPartCount = 6;
			if (parts.Length != expectedFenPartCount)
			{
				throw new ArgumentException(
					$"Invalid FEN string: expected {expectedFenPartCount} fields, found {parts.Length}.", nameof(fen));
			}

			return (parts[0], parts[1], parts[2], parts[3], parts[4], parts[5]);
		}

		private static CastlingRights ParseCastlingRights(string castlingPart)
		{
			var rights = CastlingRights.None;
			if (castlingPart.Contains('K'))
			{
				rights |= CastlingRights.WhiteKingside;
			}

			if (castlingPart.Contains('Q'))
			{
				rights |= CastlingRights.WhiteQueenside;
			}

			if (castlingPart.Contains('k'))
			{
				rights |= CastlingRights.BlackKingside;
			}

			if (castlingPart.Contains('q'))
			{
				rights |= CastlingRights.BlackQueenside;
			}

			return rights;
		}

		private static int ParseFullMoveNumber(string fullMovePart)
		{
			if (!int.TryParse(fullMovePart, out int value) || value <= 0)
			{
				throw new ArgumentException($"Invalid full-move number value: {fullMovePart}");
			}

			return value;
		}

		private static int ParseHalfMoveClock(string halfMovePart)
		{
			if (!int.TryParse(halfMovePart, out int value) || value < 0)
			{
				throw new ArgumentException($"Invalid half-move clock value: {halfMovePart}");
			}

			return value;
		}

		private static Piece FenCharToPiece(char fenChar)
		{
			PieceColor color = char.IsUpper(fenChar) ? PieceColor.White : PieceColor.Black;
			PieceType type = char.ToLower(fenChar) switch
			{
				'p' => PieceType.Pawn,
				'n' => PieceType.Knight,
				'b' => PieceType.Bishop,
				'r' => PieceType.Rook,
				'q' => PieceType.Queen,
				'k' => PieceType.King,
				_   => throw new ArgumentException($"Invalid FEN piece character: {fenChar}")
			};

			return new Piece(type, color);
		}

		private static Piece[,] ParsePiecePlacement(string placementPart)
		{
			var board = new Piece[BoardSize, BoardSize];
			int row   = 0, col = 0;

			foreach (char character in placementPart)
			{
				if (row >= BoardSize)
				{
					throw new ArgumentException($"Invalid FEN: Too many rows defined. Expected {BoardSize}.");
				}

				switch (character)
				{
					case '/':
						if (col != BoardSize)
						{
							throw new ArgumentException(
								$"Invalid FEN: Row {row + 1} is not completely defined. It has {col} squares instead of {BoardSize}.");
						}

						row++;
						col = 0;
						break;
					case >= '1' and <= '8' when char.IsDigit(character):
						col += (int)char.GetNumericValue(character);
						if (col > BoardSize)
						{
							throw new ArgumentException(
								$"Invalid FEN: Row {row + 1} contains more than {BoardSize} squares.");
						}

						break;
					default:
					{
						if (col >= BoardSize)
						{
							throw new ArgumentException(
								$"Invalid FEN: Row {row + 1} contains more than {BoardSize} squares.");
						}

						board[row, col] = FenCharToPiece(character);
						col++;
						break;
					}
				}
			}

			if (row != BoardSize - 1)
			{
				throw new ArgumentException(
					$"Invalid FEN: Not enough rows defined. Expected {BoardSize}, but found {row + 1}.");
			}

			if (col != BoardSize)
			{
				throw new ArgumentException(
					$"Invalid FEN: The last row is not completely defined. It has {col} squares instead of {BoardSize}.");
			}

			return board;
		}

		private static PieceColor ParseActiveColor(string colorPart) => colorPart switch
		{
			"w" => PieceColor.White,
			"b" => PieceColor.Black,
			_   => throw new ArgumentException($"Invalid active color specifier: {colorPart}")
		};

		private static Position? ParseEnPassantTarget(string enPassantPart)
		{
			if (enPassantPart == "-")
			{
				return null;
			}

			return new Position(enPassantPart);
		}
	}
}
