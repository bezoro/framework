using System;
using Bezoro.Chess.Domain;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Infrastructure.Fen
{
	public static class FenParser
	{
		public static GameState Parse(string fen)
		{
			var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);

			// A valid FEN always contains exactly six space-separated fields.
			if (parts.Length != 6)
				throw new ArgumentException(
					$"Invalid FEN string: expected 6 fields, found {parts.Length}.", nameof(fen));

			var piecePositions  = ParsePiecePlacement(parts[0]);
			var activeColor     = ParseActiveColor(parts[1]);
			var castlingRights  = ParseCastlingRights(parts[2]);
			var enPassantTarget = ParseEnPassantTarget(parts[3]);
			var halfMoveClock   = ParseHalfMoveClock(parts[4]);
			var fullMoveNumber  = ParseFullMoveNumber(parts[5]);

			return new()
			{
				PiecePositions        = piecePositions,
				ActiveColor           = activeColor,
				Castling              = castlingRights,
				EnPassantTargetSquare = enPassantTarget,
				HalfMoveClock         = halfMoveClock,
				FullMoveNumber        = fullMoveNumber
			};
		}

		// ----- unchanged helpers below -----

		private static CastlingRights ParseCastlingRights(string castlingPart)
		{
			var rights                             = CastlingRights.None;
			if (castlingPart.Contains('K')) rights |= CastlingRights.WhiteKingside;
			if (castlingPart.Contains('Q')) rights |= CastlingRights.WhiteQueenside;
			if (castlingPart.Contains('k')) rights |= CastlingRights.BlackKingside;
			if (castlingPart.Contains('q')) rights |= CastlingRights.BlackQueenside;
			return rights;
		}

		private static int ParseFullMoveNumber(string fullMovePart)
		{
			if (!int.TryParse(fullMovePart, out var value) || value <= 0)
				throw new ArgumentException($"Invalid full-move number value: {fullMovePart}");

			return value;
		}

		private static int ParseHalfMoveClock(string halfMovePart)
		{
			if (!int.TryParse(halfMovePart, out var value) || value < 0)
				throw new ArgumentException($"Invalid half-move clock value: {halfMovePart}");

			return value;
		}

		private static Piece FenCharToPiece(char fenChar)
		{
			var color = char.IsUpper(fenChar) ? PieceColor.White : PieceColor.Black;
			var type = char.ToLower(fenChar) switch
			{
				'p' => PieceType.Pawn,
				'n' => PieceType.Knight,
				'b' => PieceType.Bishop,
				'r' => PieceType.Rook,
				'q' => PieceType.Queen,
				'k' => PieceType.King,
				_   => throw new ArgumentException($"Invalid FEN piece character: {fenChar}")
			};

			return new(type, color);
		}

		private static Piece[,] ParsePiecePlacement(string placementPart)
		{
			var board = new Piece[8, 8];
			int row   = 0, col = 0;

			foreach (var character in placementPart)
			{
				if (character == '/')
				{
					row++;
					col = 0;
				}
				else if (char.IsDigit(character))
				{
					col += (int)char.GetNumericValue(character);
				}
				else
				{
					board[row, col] = FenCharToPiece(character);
					col++;
				}
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
				return null;

			return new Position(enPassantPart);
		}
	}
}
